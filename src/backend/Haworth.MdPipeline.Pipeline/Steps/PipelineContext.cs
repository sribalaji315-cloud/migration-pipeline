using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Pipeline.Duck;

namespace Haworth.MdPipeline.Pipeline.Steps;

public interface IPipelineStep
{
    string Name { get; }
    IReadOnlyList<string> OutputTables { get; }
    Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct);
}

public sealed record StepResult(long RowsOut, TimeSpan Duration = default);

/// <summary>Bronze version being written by the sync steps (staging folder, published by the runner).</summary>
public sealed record BronzeTarget(
    int Version,
    string StagingDir,
    SyncMode Mode,
    string? PreviousDir,
    DateTimeOffset? PreviousMaxUpdatedAt,
    int? PreviousVersion = null);

public sealed class BronzeSyncResult
{
    public BronzeWriteResult? SwingBom { get; set; }
    public BronzeWriteResult? SwingItem { get; set; }
    public long MappingRows { get; set; }
    public int ChangedItems { get; set; }
    public int MtItems { get; set; }
    public DateTime? MaxUpdatedAt { get; set; }
    public SyncMode EffectiveMode { get; set; }
    public int? MtCarriedFromVersion { get; set; }
    public int? SwingCarriedFromVersion { get; set; }
    public int? MtScopeItems { get; set; }
}

public sealed class PipelineContext
{
    public required string RunId { get; init; }
    public required DateOnly EffectiveDate { get; init; }
    public required BomTypePolicy BomTypePolicy { get; init; }
    public required SwingInterpretation Swing { get; init; }
    public required string SilverPath { get; init; }

    /// <summary>Set for runs that sync sources first.</summary>
    public BronzeTarget? BronzeTarget { get; init; }

    public BronzeSyncResult Sync { get; } = new();

    /// <summary>Published (or staged during sync) Bronze folder read by Silver steps.</summary>
    public string? BronzeDir { get; set; }

    public string? ReferenceDir { get; set; }

    /// <summary>Top BOMs limiting the Migration Tool sync to their explosions; null = unscoped.</summary>
    public IReadOnlyList<string>? MtScopeTopBoms { get; init; }

    /// <summary>Optional progress sink (rows processed so far) for long-running steps; set by the runner.</summary>
    public Func<long, Task>? Progress { get; set; }

    /// <summary>SQLite state for ReadinessStep (set by the runner after the type-decision sync).</summary>
    public IReadOnlyList<TypeDecisionInput> TypeDecisions { get; set; } = [];
    public IReadOnlyList<string> ExportedItems { get; set; } = [];

    /// <summary>Set for wave export runs (W1/W2); <see cref="SilverPath"/> is then the source analysis' Silver.</summary>
    public WaveExportTarget? Wave { get; init; }

    public string RequireBronzeDir() =>
        BronzeDir ?? throw new InvalidOperationException("No Bronze version is pinned for this run.");
}
