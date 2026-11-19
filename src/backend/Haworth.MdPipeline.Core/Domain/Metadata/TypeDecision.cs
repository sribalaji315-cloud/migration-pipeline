using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class TypeDecision
{
    public string ItemId { get; set; } = string.Empty;
    public TypeDecisionStatus Decision { get; set; }
    public string InheritedAttrsAtDecision { get; set; } = string.Empty;
    public string? SwingTypeCodeAtDecision { get; set; }
    public string LastSeenRunId { get; set; } = string.Empty;
    public string? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Note { get; set; }
}
