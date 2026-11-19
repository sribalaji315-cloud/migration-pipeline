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

/// <summary>Read-only queries over the Silver DuckDB of the latest completed analysis run.</summary>
public sealed class SilverQueryService(MdPipelineDbContext db, IOptions<PipelineOptions> options)
{
    public async Task<ItemTypeDto> ItemTypeAsync(string itemId, CancellationToken ct)
    {
        var run = await LatestAnalysisAsync(ct) ?? throw new NotFoundException("No completed analysis run exists.");
        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        var row = duck.Query(SqlScriptLoader.Load("q_item_type.sql"), itemId.Trim()).SingleOrDefault()
            ?? throw new NotFoundException($"Item '{itemId}' has no GSAR00 record in run {run.RunId}.");
        return new ItemTypeDto(
            (string)row["item_id"]!, "GSAR00", "ARVAKZ (ARMONR blank)", row["variant_flags_raw"] as string, row["type_code"] as int?,
            (string)row["source_type"]!, (bool)row["is_ambiguous"]!, Convert.ToInt32(row["model_count"]), run.RunId);
    }

    public async Task<IReadOnlyList<BomLineDto>> BomLinesAsync(string parentItemId, CancellationToken ct)
    {
        var run = await LatestAnalysisAsync(ct) ?? throw new NotFoundException("No completed analysis run exists.");
        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        return duck.Query(SqlScriptLoader.Load("q_bom_lines.sql"), parentItemId.Trim())
            .Select(r => new BomLineDto(
                (string)r["bom_line_id"]!, (string)r["parent_item_id"]!, r["child_item_id"] as string, r["bom_type"] as string,
                r["bom_position"] as string, r["child_model_no"] as string, r["quantity"] as string, r["quantity_raw"] as string,
                r["unit"] as string, r["is_pseudo_line"] as bool?, r["pseudo_raw"] as string, r["date_in"] as DateOnly?,
                r["date_out"] as DateOnly?, r["is_effective"] as bool?, (bool)r["included_by_policy"]!, r["bom_condition_id"] as string,
                "GSST00"))
            .ToList();
    }

    public async Task<PagedResult<BomSummaryDto>> BomsAsync(string? q, bool topOnly, int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = RunService.Page(limit, offset);
        var run = await RequireBomSilverAsync(ct);
        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        var rows = duck.Query(SqlScriptLoader.Load("q_boms.sql"), (q ?? string.Empty).Trim(), topOnly, (long)limit, (long)offset);
        var total = rows.Count > 0 ? Convert.ToInt32(rows[0]["total"]) : 0;
        if (rows.Count == 0 && offset > 0)
        {
            total = Convert.ToInt32(duck.Query(SqlScriptLoader.Load("q_boms.sql"), (q ?? string.Empty).Trim(), topOnly, 1L, 0L)
                .Select(r => r["total"]).FirstOrDefault() ?? 0);
        }

        return new PagedResult<BomSummaryDto>(
            rows.Select(r => new BomSummaryDto((string)r["item_id"]!, r["description"] as string,
                Convert.ToInt64(r["line_count"]), Convert.ToInt64(r["child_count"]), (bool)r["is_top"]!)).ToList(),
            total, limit, offset);
    }

    /// <summary>Selected BOMs + all descendants over every GSST00 line of the latest analysis (same rule as the import).</summary>
    public async Task<BomScopePreviewDto> ScopePreviewAsync(IReadOnlyList<string> topBoms, CancellationToken ct)
    {
        var run = await RequireBomSilverAsync(ct);
        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        var rows = duck.Query(SqlScriptLoader.Load("q_bom_scope.sql"), string.Join('\u001f', topBoms));
        var unknown = rows.Where(r => (bool)r["is_selected"]! && !(bool)r["is_known"]!).Select(r => (string)r["item_id"]!).ToList();
        return new BomScopePreviewDto(rows.Count(r => (bool)r["is_selected"]!), rows.Count, unknown, run.RunId);
    }

    private async Task<PipelineRun> RequireBomSilverAsync(CancellationToken ct)
    {
        var run = await LatestAnalysisAsync(ct)
            ?? throw new DomainValidationException("No completed analysis exists. Import Swing data first.");
        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        if (Convert.ToInt64(duck.Scalar(SqlScriptLoader.Load("q_has_bom_parent.sql"))) == 0)
        {
            throw new ConflictException($"Analysis {run.RunId} predates BOM selection. Run 'Analyse latest snapshot' (Overview) or import Swing data again.");
        }

        return run;
    }

    public async Task<IReadOnlyList<SourceIssueSummaryDto>> SourceIssueSummaryAsync(CancellationToken ct)
    {
        var run = await LatestAnalysisAsync(ct);
        if (run is null)
        {
            return [];
        }

        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath!, options.Value);
        return duck.Query(SqlScriptLoader.Load("q_source_issue_summary.sql"))
            .Select(r => new SourceIssueSummaryDto((string)r["issue_type"]!, (string)r["severity"]!, (string)r["fix_source"]!, Convert.ToInt64(r["issue_count"])))
            .ToList();
    }

    public async Task<PipelineRun?> LatestAnalysisAsync(CancellationToken ct)
    {
        var run = await db.PipelineRuns.AsNoTracking()
            .Where(r => r.RunType == RunType.Analysis && r.Status == RunStatus.Completed)
            .OrderByDescending(r => r.FinishedAt)
            .FirstOrDefaultAsync(ct);
        if (run is not null && (run.SilverPath is null || !File.Exists(run.SilverPath)))
        {
            throw new ConflictException($"Silver output of run {run.RunId} is missing ({run.SilverPath}).");
        }

        return run;
    }
}

public sealed class OverviewService(MdPipelineDbContext db, SilverQueryService silver, AnalysisQueryService analysis)
{
    public async Task<OverviewDto> GetAsync(CancellationToken ct)
    {
        var bronze = await db.BronzeVersions.Where(v => v.Status == BronzeVersionStatus.Published).OrderByDescending(v => v.Version).FirstOrDefaultAsync(ct);
        var reference = await db.ReferenceVersions.OrderByDescending(v => v.Version).FirstOrDefaultAsync(ct);
        var latestRun = await db.PipelineRuns.Include(r => r.Steps).OrderByDescending(r => r.QueuedAt).FirstOrDefaultAsync(ct);
        var completed = await silver.LatestAnalysisAsync(ct);
        var completedRun = completed is null ? null : await db.PipelineRuns.Include(r => r.Steps).SingleAsync(r => r.RunId == completed.RunId, ct);
        var active = await db.PipelineRuns.CountAsync(r => r.Status == RunStatus.Queued || r.Status == RunStatus.Syncing || r.Status == RunStatus.Running, ct);

        return new OverviewDto(bronze?.ToDto(), reference?.ToDto(), latestRun?.ToDto(), completedRun?.ToDto(), active,
            await silver.SourceIssueSummaryAsync(ct), await analysis.SummaryAsync(ct));
    }
}
