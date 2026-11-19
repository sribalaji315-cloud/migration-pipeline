using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class Wave
{
    public string WaveId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TopBomsJson { get; set; } = "[]";
    public DateOnly EffectiveDate { get; set; }
    public string BomTypePolicyJson { get; set; } = string.Empty;
    public int Items { get; set; }
    public WaveStatus Status { get; set; }
    public int BronzeVersion { get; set; }
    public int ReferenceVersion { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? LastExportRunId { get; set; }
}

public class ExportedItem
{
    public string ItemId { get; set; } = string.Empty;
    public string WaveId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; }
    public bool IsReexport { get; set; }
}
