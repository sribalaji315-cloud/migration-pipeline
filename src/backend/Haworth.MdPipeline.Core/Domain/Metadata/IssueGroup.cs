using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class IssueGroup
{
    public Guid Id { get; set; }
    public string GroupKey { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public FixSource FixSource { get; set; }
    public string FixTarget { get; set; } = string.Empty;
    public string FixAction { get; set; } = string.Empty;
    public int AffectedItems { get; set; }
    public int AffectedBoms { get; set; }
    public string FirstSeenRunId { get; set; } = string.Empty;
    public string LastSeenRunId { get; set; } = string.Empty;
    public string? ResolvedInRunId { get; set; }
    public IssueStatus Status { get; set; }
    public string? AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? Note { get; set; }
}
