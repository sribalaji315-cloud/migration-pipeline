namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
