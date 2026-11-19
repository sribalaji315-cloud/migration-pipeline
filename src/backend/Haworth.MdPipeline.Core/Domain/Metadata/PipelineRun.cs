using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class PipelineRun
{
    public string RunId { get; set; } = string.Empty;
    public RunType RunType { get; set; }
    public bool SyncRequested { get; set; }
    public SyncMode? SyncMode { get; set; }
    public SyncScope SyncScope { get; set; } = SyncScope.All;

    /// <summary>JSON array of top BOM item ids limiting a Migration Tool import to their BOM explosions (null = unscoped).</summary>
    public string? ScopeTopBomsJson { get; set; }
    public int? BronzeVersion { get; set; }
    public int? ReferenceVersion { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string BomTypePolicyJson { get; set; } = string.Empty;
    public string? SourceAnalysisRunId { get; set; }
    public string? WaveId { get; set; }
    public string? SilverPath { get; set; }
    public string? GoldPath { get; set; }
    public RunStatus Status { get; set; }
    public string StartedByUserId { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public List<PipelineRunStep> Steps { get; set; } = [];
}

public class PipelineRunStep
{
    public Guid Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Step { get; set; } = string.Empty;
    public string OutputTables { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public long RowsOut { get; set; }
    public double DurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}
