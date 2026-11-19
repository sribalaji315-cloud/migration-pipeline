using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Data;

namespace Haworth.MdPipeline.Api.Services;

/// <summary>Adds immutable audit entries to the current unit of work; the owning service saves them atomically.</summary>
public sealed class AuditService(MdPipelineDbContext db, TimeProvider clock)
{
    public void Write(string entityType, string entityId, string action, string actorId, string summary) =>
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ChangedAt = clock.GetUtcNow().UtcDateTime,
            ChangedByUserId = actorId,
            Summary = summary
        });
}
