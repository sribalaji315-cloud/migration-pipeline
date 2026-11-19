using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class BronzeVersion
{
    public int Version { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
    public DateTime? MaxUpdatedAt { get; set; }
    public SyncMode Mode { get; set; }
    public int ChangedItems { get; set; }
    public long MappingRows { get; set; }
    public int MtItems { get; set; }

    /// <summary>Set when a Swing-only import reused the Migration Tool data of an earlier snapshot.</summary>
    public int? MtCarriedFromVersion { get; set; }

    /// <summary>Set when a Migration Tool-only import reused the Swing data of an earlier snapshot.</summary>
    public int? SwingCarriedFromVersion { get; set; }

    /// <summary>For BOM-scoped Migration Tool imports: JSON array of the selected top BOMs and the number of items refreshed.</summary>
    public string? MtScopeTopBomsJson { get; set; }
    public int? MtScopeItems { get; set; }
    public int SwingItems { get; set; }
    public DateTime? SwingExtractedAt { get; set; }
    public string? SwingItemHash { get; set; }
    public long SwingBomRows { get; set; }
    public DateTime? SwingBomExtractedAt { get; set; }
    public string? SwingBomHash { get; set; }
    public BronzeVersionStatus Status { get; set; }
    public string? CreatedByRunId { get; set; }
}
