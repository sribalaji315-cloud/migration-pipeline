using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Api.Services;

/// <summary>Class model, readiness and type change queries over the Silver of the latest completed analysis (steps 3-12).</summary>
public sealed class AnalysisQueryService(MdPipelineDbContext db, SilverQueryService silver, IOptions<PipelineOptions> options)
{
    public const int MaxTreeNodes = 5000;

    /// <summary>Opens the latest analysis' Silver; 409 when it was produced without the analysis steps (no Bluestar reference).</summary>
    public async Task<(PipelineRun Run, DuckDbExecutor Duck)> OpenAsync(CancellationToken ct)
    {
        var run = await silver.LatestAnalysisAsync(ct) ?? throw new NotFoundException("No completed analysis run exists.");
        var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        if (!HasTable(duck, "bom_readiness"))
        {
            duck.Dispose();
            throw new ConflictException($"Analysis {run.RunId} has no class model. Upload the Bluestar reference files (Reference page) and run the analysis again.");
        }

        return (run, duck);
    }

    public static bool HasTable(DuckDbExecutor duck, string table) =>
        Convert.ToInt64(duck.Query("SELECT count(*) AS n FROM information_schema.tables WHERE table_name = $1", table)[0]["n"]) > 0;

    public async Task<ItemClassDto> ItemClassAsync(string itemId, CancellationToken ct)
    {
        var (run, duck) = await OpenAsync(ct);
        using (duck)
        {
            var header = duck.Query(SqlScriptLoader.Load("q_item_class_header.sql"), itemId.Trim()).SingleOrDefault()
                ?? throw new NotFoundException($"Item '{itemId}' is not in the analysis scope of run {run.RunId} (not imported from the Migration Tool).");
            var attributes = duck.Query(SqlScriptLoader.Load("q_item_class.sql"), itemId.Trim())
                .Select(r => new ItemClassAttributeDto(
                    (string)r["attribute_id"]!, Pascal((string)r["value_source"]!), Pascal((string)r["attribute_source"]!),
                    r["global_variable"] as string, r["value_list_id"] as string, ToLong(r["value_list_size"]), r["value_type"] as string,
                    Convert.ToInt64(r["n_used"]), ToLong(r["covered"]), Pascal((string)r["status"]!), r["inherited_from"] as string, r["note"] as string))
                .ToList();
            var counts = new AttributeCountsDto(
                attributes.Count(a => a.Status == "Go"), attributes.Count(a => a.Status == "Highlight"), attributes.Count(a => a.Status == "Review"));
            var itemStatus = counts.Review > 0 ? "Review" : counts.Highlight > 0 ? "Highlight" : "Go";
            return new ItemClassDto(
                (string)header["item_id"]!, (string)header["class_id"]!, (string)header["parent_class_id"]!, header["description"] as string,
                itemStatus, header["readiness"] as string, header["resolved_type"] as string, header["type_changed"] as bool? ?? false,
                counts, attributes, run.RunId);
        }
    }

    public async Task<MissingValuesDto> MissingValuesAsync(string itemId, string attributeId, CancellationToken ct)
    {
        var (run, duck) = await OpenAsync(ct);
        using (duck)
        {
            var rows = duck.Query(SqlScriptLoader.Load("q_missing_values.sql"), itemId.Trim(), attributeId.Trim());
            var first = rows.FirstOrDefault();
            return new MissingValuesDto(itemId.Trim(), attributeId.Trim(), first?["global_variable"] as string, first?["value_list_id"] as string,
                first?["value_type"] as string, first?["is_list"] as bool? ?? false, rows.Select(r => (string)r["value_code"]!).ToList(), run.RunId);
        }
    }

    /// <summary>Adds the resolved type (R16) and the stored decision to the Swing type, when the analysis has them.</summary>
    public async Task<ItemTypeDto> ExtendItemTypeAsync(ItemTypeDto type, CancellationToken ct)
    {
        var run = await silver.LatestAnalysisAsync(ct);
        if (run is null)
        {
            return type;
        }

        Dictionary<string, object?>? row;
        using (var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value))
        {
            row = HasTable(duck, "item_type_resolved")
                ? duck.Query(SqlScriptLoader.Load("q_item_type_resolved.sql"), type.ItemId).SingleOrDefault()
                : null;
        }

        if (row is null)
        {
            return type;
        }

        var attrs = (string?)row["inherited_attrs"] ?? string.Empty;
        var decision = await db.TypeDecisions.AsNoTracking().SingleOrDefaultAsync(d => d.ItemId == type.ItemId, ct);
        return type with
        {
            ResolvedType = (string)row["resolved_type"]!,
            TypeChanged = (bool)row["type_changed"]!,
            InheritedAttrs = attrs,
            Decision = decision?.Decision,
            DecisionMatches = decision is not null && decision.InheritedAttrsAtDecision == attrs
                && decision.SwingTypeCodeAtDecision == type.TypeCode?.ToString()
        };
    }

    public async Task<PagedResult<BomReadinessDto>> BomReadinessAsync(string? q, string? status, int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = RunService.Page(limit, offset);
        var (_, duck) = await OpenAsync(ct);
        using (duck)
        {
            var rows = duck.Query(SqlScriptLoader.Load("q_bom_readiness.sql"), (q ?? string.Empty).Trim(), (status ?? string.Empty).Trim().ToUpperInvariant(),
                (long)limit, (long)offset);
            var total = rows.Count > 0 ? Convert.ToInt32(rows[0]["total"]) : 0;
            if (rows.Count == 0 && offset > 0)
            {
                total = Convert.ToInt32(duck.Query(SqlScriptLoader.Load("q_bom_readiness.sql"), (q ?? string.Empty).Trim(),
                    (status ?? string.Empty).Trim().ToUpperInvariant(), 1L, 0L).Select(r => r["total"]).FirstOrDefault() ?? 0);
            }

            return new PagedResult<BomReadinessDto>(rows.Select(r => new BomReadinessDto(
                (string)r["top_item_id"]!, r["description"] as string, Convert.ToInt64(r["items"]), Convert.ToInt64(r["blocked_items"]),
                Convert.ToInt64(r["review_items"]), Convert.ToInt64(r["decision_items"]), Convert.ToInt64(r["already_exported_items"]),
                Convert.ToInt64(r["not_imported_items"]), (string)r["status"]!)).ToList(), total, limit, offset);
        }
    }

    public async Task<BomTreeDto> BomTreeAsync(string topItemId, CancellationToken ct)
    {
        var (run, duck) = await OpenAsync(ct);
        using (duck)
        {
            var top = topItemId.Trim();
            var nodes = duck.Query(SqlScriptLoader.Load("q_bom_tree_nodes.sql"), top, (long)MaxTreeNodes);
            if (nodes.Count == 0)
            {
                throw new NotFoundException($"'{top}' is not a top BOM in analysis {run.RunId}.");
            }

            var total = Convert.ToInt32(nodes[0]["total"]);
            var ids = nodes.Select(n => (string)n["item_id"]!).ToHashSet(StringComparer.Ordinal);
            var edges = duck.Query(SqlScriptLoader.Load("q_bom_tree_edges.sql"), top)
                .Select(e => new BomTreeEdgeDto((string)e["parent_item_id"]!, (string)e["child_item_id"]!))
                .Where(e => ids.Contains(e.ParentItemId) && ids.Contains(e.ChildItemId))
                .ToList();
            return new BomTreeDto(top, total, total > nodes.Count,
                nodes.Select(n => new BomTreeNodeDto((string)n["item_id"]!, n["description"] as string, (string)n["status"]!,
                    n["resolved_type"] as string, (bool)n["type_changed"]!)).ToList(), edges, run.RunId);
        }
    }

    public async Task<PagedResult<TypeChangeDto>> TypeChangesAsync(string? q, TypeDecisionStatus? decision, int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = RunService.Page(limit, offset);
        List<Dictionary<string, object?>> rows;
        var (_, duck) = await OpenAsync(ct);
        using (duck)
        {
            rows = duck.Query(SqlScriptLoader.Load("q_type_changes.sql"));
        }

        var decisions = await db.TypeDecisions.AsNoTracking().ToDictionaryAsync(d => d.ItemId, ct);
        var all = rows.Select(r =>
            {
                var itemId = (string)r["item_id"]!;
                decisions.TryGetValue(itemId, out var d);
                return new TypeChangeDto(itemId, r["description"] as string, r["swing_type_code"] as string, (string)r["source_type"]!,
                    (string)r["resolved_type"]!, (string)r["inherited_attrs"]!, Convert.ToInt64(r["affected_boms"]),
                    d?.Decision ?? TypeDecisionStatus.Pending, d?.DecidedByUserId, d?.DecidedAt, d?.Note);
            })
            .Where(t => decision is null || t.Decision == decision)
            .Where(t => string.IsNullOrWhiteSpace(q) || t.ItemId.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase)
                || (t.Description?.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        return new PagedResult<TypeChangeDto>(all.Skip(offset).Take(limit).ToList(), all.Count, limit, offset);
    }

    public async Task<AnalysisSummaryDto?> SummaryAsync(CancellationToken ct)
    {
        var run = await silver.LatestAnalysisAsync(ct);
        if (run is null)
        {
            return null;
        }

        long items;
        Dictionary<string, int> counts;
        using (var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value))
        {
            if (!HasTable(duck, "bom_readiness"))
            {
                return null;
            }

            items = duck.CountRows("mt_scope_item");
            counts = duck.Query(SqlScriptLoader.Load("q_bom_readiness_counts.sql"))
                .ToDictionary(r => (string)r["status"]!, r => Convert.ToInt32(r["boms"]));
        }

        var open = db.IssueGroups.AsNoTracking().Where(g => g.Status != IssueStatus.Resolved);
        var top = await open.Where(g => g.Severity == Severity.Block && g.Status == IssueStatus.Open)
            .OrderByDescending(g => g.AffectedBoms).ThenByDescending(g => g.AffectedItems).ThenBy(g => g.GroupKey)
            .Take(5).ToListAsync(ct);
        return new AnalysisSummaryDto(run.RunId, items,
            new BomReadinessCountsDto(counts.GetValueOrDefault("READY"), counts.GetValueOrDefault("DECISION"), counts.GetValueOrDefault("REVIEW"),
                counts.GetValueOrDefault("BLOCKED"), counts.GetValueOrDefault("EXPORTED")),
            await open.CountAsync(ct),
            await db.TypeDecisions.CountAsync(d => d.Decision == TypeDecisionStatus.Pending && d.LastSeenRunId == run.RunId, ct),
            top.Select(g => g.ToDto()).ToList());
    }

    private static long? ToLong(object? value) => value is null ? null : Convert.ToInt64(value);

    // Silver uses SQL-style codes (HIGHLIGHT, MAPPING_API); the API contract uses PascalCase (Highlight, MappingApi).
    public static string Pascal(string code) =>
        string.Concat(code.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
}
