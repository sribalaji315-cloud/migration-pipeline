namespace Haworth.MdPipeline.Core.Interfaces;

/// <summary>
/// One Migration Tool workspace-mapping row. Field names follow the live API (verified against GET /workspace-mappings/{itemId});
/// flags are kept as raw text (e.g. inherited = "Yes"/"No") and <see cref="RawJson"/> preserves the complete source object.
/// </summary>
public sealed record MtMappingRow(
    string LegacyItemId,
    string? LegacyFeatureId,
    string? LegacyValue,
    string? NewAttributeId,
    string? NewValue,
    string? AttributeType,
    string? Condition,
    string? SimplifiedLegacyCondition,
    string? TargetCondition,
    string? Formula,
    string? MappedFrom,
    string? ValueStatus,
    string? Feasibility,
    bool? LegacyReachable,
    string? Inherited,
    string? PredictionStatus,
    string? PredictionConfidence,
    string? PredictionMatchType,
    string? PredictionEvidence,
    string? SignedOnBy,
    DateTimeOffset? SignedOnAt,
    DateTimeOffset? UpdatedAt,
    string RawJson);

/// <summary>Item header from POST /bom/items/by-ids. <see cref="RawJson"/> keeps the full object (incl. features).</summary>
public sealed record MtItemHeader(
    string ItemId,
    string? Description,
    string? Category,
    string? ProductType,
    string? Priority,
    bool? ApprovedForMigration,
    string RawJson);

/// <summary>Read-only Migration Tool client. Only GET, POST /auth/* and POST /bom/items/by-ids are allowed.</summary>
public interface IMigrationToolClient
{
    bool SupportsBulkMappings { get; }

    IAsyncEnumerable<MtMappingRow> GetMappingsBulkAsync(DateTimeOffset? updatedSince, CancellationToken ct);

    Task<IReadOnlyList<MtMappingRow>> GetItemMappingsAsync(string itemId, CancellationToken ct);

    Task<IReadOnlyList<MtItemHeader>> GetItemsByIdsAsync(IReadOnlyList<string> itemIds, CancellationToken ct);

    /// <summary>Every item loaded in the Migration Tool (GET /bom/items, paged), with its header (approval etc.).</summary>
    Task<IReadOnlyList<MtItemHeader>> GetAllItemsAsync(Func<long, Task>? progress, CancellationToken ct);

    Task<SourceHealth> CheckAsync(CancellationToken ct);
}
