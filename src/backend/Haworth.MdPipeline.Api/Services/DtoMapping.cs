using System.Text.Json;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Rules;

namespace Haworth.MdPipeline.Api.Services;

public static class DtoMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RunDto ToDto(this PipelineRun run)
    {
        var policy = BomTypePolicy.FromJson(run.BomTypePolicyJson);
        return new RunDto(
            run.RunId, run.RunType, run.Status, run.SyncRequested, run.SyncMode, run.SyncScope, TopBoms(run.ScopeTopBomsJson), run.BronzeVersion, run.ReferenceVersion,
            run.EffectiveDate, new BomTypePolicyDto(policy.Mode, policy.Types), run.StartedByUserId, run.QueuedAt,
            run.StartedAt, run.FinishedAt, run.ErrorMessage,
            run.Steps.OrderBy(s => s.Sequence)
                .Select(s => new RunStepDto(s.Sequence, s.Step, s.OutputTables, s.Status, s.RowsOut, s.DurationSeconds, s.ErrorMessage))
                .ToList(),
            run.WaveId, run.SourceAnalysisRunId);
    }

    public static IssueGroupDto ToDto(this IssueGroup g) => new(
        g.Id, g.GroupKey, g.IssueType, g.Severity, g.FixSource, g.FixTarget, g.FixAction, g.AffectedItems, g.AffectedBoms,
        g.FirstSeenRunId, g.LastSeenRunId, g.ResolvedInRunId, g.Status, g.AcknowledgedByUserId, g.AcknowledgedAt, g.Note);

    public static WaveDto ToDto(this Wave w) => new(
        w.WaveId, w.Name, TopBoms(w.TopBomsJson) ?? [], w.EffectiveDate, w.Items, w.Status, w.BronzeVersion, w.ReferenceVersion,
        w.CreatedByUserId, w.CreatedAt, w.LastExportRunId);

    public static BronzeVersionDto ToDto(this BronzeVersion v) => new(
        v.Version, v.Status, v.Mode, v.SyncedAt, v.MaxUpdatedAt, v.ChangedItems, v.MappingRows, v.MtItems, v.MtCarriedFromVersion, v.SwingCarriedFromVersion,
        TopBoms(v.MtScopeTopBomsJson), v.MtScopeItems, v.SwingItems,
        v.SwingExtractedAt, v.SwingItemHash, v.SwingBomRows, v.SwingBomExtractedAt, v.SwingBomHash, v.CreatedByRunId);

    public static ReferenceVersionDto ToDto(this ReferenceVersion v) => new(
        v.Version, v.UploadedAt, v.UploadedByUserId,
        JsonSerializer.Deserialize<List<ReferenceFileDto>>(v.FilesJson, Json) ?? []);

    public static string ToFilesJson(IEnumerable<ReferenceFileDto> files) => JsonSerializer.Serialize(files, Json);

    public static IReadOnlyList<string>? TopBoms(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<List<string>>(json, Json);
}
