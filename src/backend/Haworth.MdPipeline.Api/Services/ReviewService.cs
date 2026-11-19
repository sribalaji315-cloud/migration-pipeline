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

/// <summary>Issue groups (SQLite state + Silver detail) and Approver type decisions.</summary>
public sealed class ReviewService(
    MdPipelineDbContext db,
    AuditService audit,
    AnalysisQueryService analysis,
    TimeProvider clock,
    IOptions<PipelineOptions> options)
{
    public sealed record IssueFilter(Severity? Severity, FixSource? FixSource, string? IssueType, IssueStatus? Status, string? Q, string? Bom);

    public async Task<PagedResult<IssueGroupDto>> GroupsAsync(IssueFilter filter, int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = RunService.Page(limit, offset);
        var query = db.IssueGroups.AsNoTracking();
        query = filter.Status is { } status ? query.Where(g => g.Status == status) : query.Where(g => g.Status != IssueStatus.Resolved);
        if (filter.Severity is { } severity)
        {
            query = query.Where(g => g.Severity == severity);
        }

        if (filter.FixSource is { } fixSource)
        {
            query = query.Where(g => g.FixSource == fixSource);
        }

        if (!string.IsNullOrWhiteSpace(filter.IssueType))
        {
            query = query.Where(g => g.IssueType == filter.IssueType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(g => g.GroupKey.Contains(q) || g.FixTarget.Contains(q) || g.IssueType.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(filter.Bom))
        {
            var keys = await GroupKeysForBomAsync(filter.Bom.Trim(), ct);
            var all = (await query.ToListAsync(ct)).Where(g => keys.Contains((g.GroupKey, g.IssueType))).ToList();
            return new PagedResult<IssueGroupDto>(Order(all.AsQueryable()).Skip(offset).Take(limit).Select(g => g.ToDto()).ToList(), all.Count, limit, offset);
        }

        var total = await query.CountAsync(ct);
        var page = await Order(query).Skip(offset).Take(limit).ToListAsync(ct);
        return new PagedResult<IssueGroupDto>(page.Select(g => g.ToDto()).ToList(), total, limit, offset);
    }

    // Severity is stored as text, so rank it explicitly (Block first) instead of sorting alphabetically.
    private static IQueryable<IssueGroup> Order(IQueryable<IssueGroup> query) =>
        query.OrderBy(g => g.Severity == Severity.Block ? 0 : g.Severity == Severity.Review ? 1 : g.Severity == Severity.Approve ? 2 : g.Severity == Severity.Warn ? 3 : 4)
            .ThenByDescending(g => g.AffectedBoms).ThenByDescending(g => g.AffectedItems)
            .ThenBy(g => g.IssueType).ThenBy(g => g.GroupKey);

    private async Task<HashSet<(string, string)>> GroupKeysForBomAsync(string bom, CancellationToken ct)
    {
        var (_, duck) = await analysis.OpenAsync(ct);
        using (duck)
        {
            return duck.Query(SqlScriptLoader.Load("q_issue_groups_for_bom.sql"), bom)
                .Select(r => ((string)r["group_key"]!, (string)r["issue_type"]!))
                .ToHashSet();
        }
    }

    public async Task<IReadOnlyList<IssueSummaryDto>> SummaryAsync(CancellationToken ct)
    {
        var rows = await db.IssueGroups.AsNoTracking()
            .Where(g => g.Status != IssueStatus.Resolved)
            .GroupBy(g => new { g.FixSource, g.Severity })
            .Select(g => new { g.Key.FixSource, g.Key.Severity, Groups = g.Count(), Items = g.Sum(x => x.AffectedItems) })
            .ToListAsync(ct);
        return rows.OrderBy(r => r.FixSource).ThenBy(r => r.Severity)
            .Select(r => new IssueSummaryDto(r.FixSource, r.Severity, r.Groups, r.Items)).ToList();
    }

    public async Task<IssueGroupDto> GroupAsync(Guid id, CancellationToken ct) => (await FindAsync(id, ct)).ToDto();

    /// <summary>Occurrences from the Silver of the run that last saw the group (also works for resolved groups).</summary>
    public async Task<PagedResult<IssueItemDto>> GroupItemsAsync(Guid id, int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = RunService.Page(limit, offset);
        var group = await FindAsync(id, ct);
        var run = await db.PipelineRuns.AsNoTracking().SingleOrDefaultAsync(r => r.RunId == group.LastSeenRunId, ct);
        if (run?.SilverPath is null || !File.Exists(run.SilverPath))
        {
            throw new ConflictException($"Silver output of run {group.LastSeenRunId} is no longer available.");
        }

        using var duck = DuckDbExecutor.OpenReadOnly(run.SilverPath, options.Value);
        var rows = duck.Query(SqlScriptLoader.Load("q_issue_items.sql"), group.GroupKey, group.IssueType, (long)limit, (long)offset);
        var total = rows.Count > 0 ? Convert.ToInt32(rows[0]["total"]) : group.AffectedItems;
        return new PagedResult<IssueItemDto>(rows.Select(r => new IssueItemDto(
            r["item_id"] as string, r["parent_item_id"] as string, r["legacy_feature_id"] as string, r["legacy_value"] as string,
            r["attribute_id"] as string, r["value_code"] as string, r["global_variable"] as string, r["value_list_id"] as string,
            r["detail"] as string)).ToList(), total, limit, offset);
    }

    /// <summary>Only WARN/INFO can be acknowledged; BLOCK/REVIEW/APPROVE must be fixed at the source (or decided).</summary>
    public async Task<IssueGroupDto> AcknowledgeAsync(Guid id, string? note, string actorId, CancellationToken ct)
    {
        var group = await FindAsync(id, ct);
        if (group.Severity is not (Severity.Warn or Severity.Info))
        {
            throw new DomainValidationException($"{group.Severity} issues cannot be acknowledged; fix them in {group.FixSource}.");
        }

        if (group.Status != IssueStatus.Open)
        {
            throw new ConflictException($"Issue group is {group.Status}.");
        }

        group.Status = IssueStatus.Acknowledged;
        group.AcknowledgedByUserId = actorId;
        group.AcknowledgedAt = clock.GetUtcNow().UtcDateTime;
        group.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        audit.Write(nameof(IssueGroup), group.Id.ToString(), "Acknowledged", actorId, $"{group.IssueType} {group.GroupKey}: {group.Note}");
        await db.SaveChangesAsync(ct);
        return group.ToDto();
    }

    /// <summary>
    /// Records an Approver decision against the current inherited attributes / Swing code of the latest analysis.
    /// The decision is reset to Pending by the next analysis when either changes.
    /// </summary>
    public async Task<TypeDecisionResultDto> DecideAsync(TypeDecisionRequestDto request, string actorId, CancellationToken ct)
    {
        if (request.Decision == TypeDecisionStatus.Pending)
        {
            throw new DomainValidationException("Decision must be Approved or Rejected.");
        }

        var ids = (request.ItemIds ?? []).Select(i => i?.Trim() ?? string.Empty).Where(i => i.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            throw new DomainValidationException("Select at least one item.");
        }

        if (request.Decision == TypeDecisionStatus.Rejected && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new DomainValidationException("A note is required when rejecting a type change.");
        }

        var byId = (await AllTypeChangesAsync(ct)).ToDictionary(c => c.ItemId, StringComparer.Ordinal);
        var decisions = await db.TypeDecisions.Where(d => ids.Contains(d.ItemId)).ToDictionaryAsync(d => d.ItemId, ct);
        var notFound = new List<string>();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var change))
            {
                notFound.Add(id);
                continue;
            }

            if (!decisions.TryGetValue(id, out var decision))
            {
                decision = new TypeDecision { ItemId = id };
                db.TypeDecisions.Add(decision);
            }

            decision.Decision = request.Decision;
            decision.InheritedAttrsAtDecision = change.InheritedAttrs;
            decision.SwingTypeCodeAtDecision = change.SwingTypeCode;
            decision.DecidedByUserId = actorId;
            decision.DecidedAt = now;
            decision.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            audit.Write(nameof(TypeDecision), id, request.Decision.ToString(), actorId,
                $"FIXED → CONFIGURABLE {request.Decision.ToString().ToLowerInvariant()} (inherited: {change.InheritedAttrs}, Swing type {change.SwingTypeCode}).");
        }

        await db.SaveChangesAsync(ct);
        return new TypeDecisionResultDto(ids.Count - notFound.Count, notFound);
    }

    private async Task<IReadOnlyList<TypeChangeDto>> AllTypeChangesAsync(CancellationToken ct)
    {
        var all = new List<TypeChangeDto>();
        for (var offset = 0; ; offset += RunService.MaxLimit)
        {
            var page = await analysis.TypeChangesAsync(null, null, RunService.MaxLimit, offset, ct);
            all.AddRange(page.Items);
            if (all.Count >= page.Total || page.Items.Count == 0)
            {
                return all;
            }
        }
    }

    private async Task<IssueGroup> FindAsync(Guid id, CancellationToken ct) =>
        await db.IssueGroups.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException($"Issue group '{id}' was not found.");
}
