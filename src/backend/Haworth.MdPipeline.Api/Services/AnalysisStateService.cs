using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Api.Services;

/// <summary>Carries analysis results that need human state (type decisions, issue acknowledgement) into SQLite.</summary>
public sealed class AnalysisStateService(MdPipelineDbContext db, AuditService audit, IOptions<PipelineOptions> options)
{
    /// <summary>
    /// R16: every FIXED→CONFIGURABLE item gets a decision row. A decision made for different inherited attributes or a
    /// different Swing type code is reset to Pending, so an approval never silently covers a changed situation.
    /// </summary>
    public async Task<IReadOnlyList<TypeDecisionInput>> SyncTypeDecisionsAsync(string runId, string silverPath, CancellationToken ct)
    {
        List<Dictionary<string, object?>> rows;
        using (var duck = DuckDbExecutor.OpenReadOnly(silverPath, options.Value))
        {
            rows = duck.Query("SELECT item_id, inherited_attrs, CAST(swing_type_code AS VARCHAR) AS code FROM item_type_resolved WHERE type_changed");
        }

        var existing = await db.TypeDecisions.ToDictionaryAsync(d => d.ItemId, ct);
        var result = new List<TypeDecisionInput>(rows.Count);
        foreach (var row in rows)
        {
            var itemId = (string)row["item_id"]!;
            var attrs = (string?)row["inherited_attrs"] ?? string.Empty;
            var code = (string?)row["code"];
            if (!existing.TryGetValue(itemId, out var decision))
            {
                decision = new TypeDecision { ItemId = itemId, Decision = TypeDecisionStatus.Pending };
                db.TypeDecisions.Add(decision);
            }
            else if (decision.InheritedAttrsAtDecision != attrs || decision.SwingTypeCodeAtDecision != code)
            {
                if (decision.Decision != TypeDecisionStatus.Pending)
                {
                    audit.Write(nameof(TypeDecision), itemId, "Reset", "system",
                        $"{decision.Decision} decision reset to Pending: inherited attributes '{decision.InheritedAttrsAtDecision}' → '{attrs}', Swing type {decision.SwingTypeCodeAtDecision} → {code}.");
                }

                decision.Decision = TypeDecisionStatus.Pending;
                decision.DecidedByUserId = null;
                decision.DecidedAt = null;
            }

            decision.InheritedAttrsAtDecision = attrs;
            decision.SwingTypeCodeAtDecision = code;
            decision.LastSeenRunId = runId;
            result.Add(new TypeDecisionInput(itemId, decision.Decision.ToString(), attrs, code));
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Open new groups, reopen resolved ones that came back, resolve groups no longer found. Acknowledged stays acknowledged.</summary>
    public async Task SyncIssueGroupsAsync(string runId, string silverPath, CancellationToken ct)
    {
        List<Dictionary<string, object?>> rows;
        using (var duck = DuckDbExecutor.OpenReadOnly(silverPath, options.Value))
        {
            rows = duck.Query("SELECT group_key, issue_type, severity, fix_source, fix_target, fix_action, affected_items, affected_boms FROM review_issue_groups");
        }

        var existing = await db.IssueGroups.ToDictionaryAsync(g => (g.GroupKey, g.IssueType), ct);
        var seen = new HashSet<(string, string)>();
        foreach (var row in rows)
        {
            var key = ((string)row["group_key"]!, (string)row["issue_type"]!);
            seen.Add(key);
            if (!existing.TryGetValue(key, out var group))
            {
                group = new IssueGroup
                {
                    Id = Guid.NewGuid(),
                    GroupKey = key.Item1,
                    IssueType = key.Item2,
                    FirstSeenRunId = runId,
                    Status = IssueStatus.Open
                };
                db.IssueGroups.Add(group);
            }
            else if (group.Status == IssueStatus.Resolved)
            {
                group.Status = IssueStatus.Open;
                group.ResolvedInRunId = null;
            }

            group.Severity = Enum.Parse<Severity>((string)row["severity"]!);
            group.FixSource = Enum.Parse<FixSource>((string)row["fix_source"]!);
            group.FixTarget = (string?)row["fix_target"] ?? string.Empty;
            group.FixAction = (string?)row["fix_action"] ?? string.Empty;
            group.AffectedItems = Convert.ToInt32(row["affected_items"]);
            group.AffectedBoms = Convert.ToInt32(row["affected_boms"]);
            group.LastSeenRunId = runId;
        }

        foreach (var (key, group) in existing)
        {
            if (!seen.Contains(key) && group.Status != IssueStatus.Resolved)
            {
                group.Status = IssueStatus.Resolved;
                group.ResolvedInRunId = runId;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
