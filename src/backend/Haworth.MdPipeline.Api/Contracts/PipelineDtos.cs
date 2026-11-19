using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Api.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);

public sealed record AuthLoginRequest(string Email, string Password);

public sealed record AuthLoginResponse(string Token, string Email, string DisplayName, IReadOnlyList<string> Roles);

public sealed record BomTypePolicyDto(BomTypePolicyMode Mode, IReadOnlyList<string>? Types);

public sealed record RunRequestDto(DateOnly? EffectiveDate, BomTypePolicyDto? BomTypePolicy);

/// <summary>Migration Tool import; <see cref="TopBoms"/> limits it to the selected BOMs' explosions (top + all descendants).</summary>
public sealed record MigrationToolImportRequestDto(DateOnly? EffectiveDate, BomTypePolicyDto? BomTypePolicy, IReadOnlyList<string>? TopBoms)
{
    public RunRequestDto ToRunRequest() => new(EffectiveDate, BomTypePolicy);
}

public sealed record BomSummaryDto(string ItemId, string? Description, long LineCount, long ChildCount, bool IsTop);

public sealed record BomScopePreviewRequestDto(IReadOnlyList<string> TopBoms);

public sealed record BomScopePreviewDto(int Selected, int Items, IReadOnlyList<string> Unknown, string RunId);

public sealed record RunStepDto(int Sequence, string Step, string OutputTables, StepStatus Status, long RowsOut, double DurationSeconds, string? ErrorMessage);

public sealed record RunDto(
    string RunId,
    RunType RunType,
    RunStatus Status,
    bool SyncRequested,
    SyncMode? SyncMode,
    SyncScope SyncScope,
    IReadOnlyList<string>? ScopeTopBoms,
    int? BronzeVersion,
    int? ReferenceVersion,
    DateOnly EffectiveDate,
    BomTypePolicyDto BomTypePolicy,
    string StartedByUserId,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage,
    IReadOnlyList<RunStepDto> Steps,
    string? WaveId = null,
    string? SourceAnalysisRunId = null);

public sealed record BronzeVersionDto(
    int Version,
    BronzeVersionStatus Status,
    SyncMode Mode,
    DateTime SyncedAt,
    DateTime? MaxUpdatedAt,
    int ChangedItems,
    long MappingRows,
    int MtItems,
    int? MtCarriedFromVersion,
    int? SwingCarriedFromVersion,
    IReadOnlyList<string>? MtScopeTopBoms,
    int? MtScopeItems,
    int SwingItems,
    DateTime? SwingExtractedAt,
    string? SwingItemHash,
    long SwingBomRows,
    DateTime? SwingBomExtractedAt,
    string? SwingBomHash,
    string? CreatedByRunId);

public sealed record ReferenceFileDto(string Kind, string FileName, string BronzeTable, long Rows, string Sha256);

public sealed record ReferenceVersionDto(int Version, DateTime UploadedAt, string UploadedByUserId, IReadOnlyList<ReferenceFileDto> Files);

public sealed record HealthDto(bool Healthy, IReadOnlyList<HealthCheckDto> Checks);

public sealed record HealthCheckDto(string Name, bool Healthy, string Detail);

/// <summary>Swing source configuration (no credentials) plus the latest imported snapshot.</summary>
public sealed record SwingSourceDto(
    string Mode,
    string DataSource,
    string Library,
    BronzeVersionDto? LatestSnapshot,
    RunDto? ActiveImport,
    RunDto? LastImport);

/// <summary>Migration Tool source configuration (user name / token expiry, never the password or token) plus the latest imported snapshot.</summary>
public sealed record MigrationToolSourceDto(
    string Mode,
    string BaseUrl,
    string AuthMode,
    string User,
    DateTimeOffset? TokenExpiresAt,
    bool BulkEndpoint,
    BronzeVersionDto? LatestSnapshot,
    RunDto? ActiveImport,
    RunDto? LastImport);

public sealed record OverviewDto(
    BronzeVersionDto? LatestBronze,
    ReferenceVersionDto? LatestReference,
    RunDto? LatestRun,
    RunDto? LatestCompletedAnalysis,
    int ActiveRuns,
    IReadOnlyList<SourceIssueSummaryDto> SwingIssues,
    AnalysisSummaryDto? Analysis);

public sealed record SourceIssueSummaryDto(string IssueType, string Severity, string FixSource, long Count);

public sealed record ItemTypeDto(
    string ItemId,
    string SourceTable,
    string SourceColumn,
    string? RawCode,
    int? TypeCode,
    string SourceType,
    bool IsAmbiguous,
    int ModelCount,
    string RunId,
    string? ResolvedType = null,
    bool TypeChanged = false,
    string? InheritedAttrs = null,
    TypeDecisionStatus? Decision = null,
    bool DecisionMatches = false);

public sealed record BomLineDto(
    string BomLineId,
    string ParentItemId,
    string? ChildItemId,
    string? BomType,
    string? BomPosition,
    string? ChildModelNo,
    string? Quantity,
    string? QuantityRaw,
    string? Unit,
    bool? IsPseudoLine,
    string? PseudoRaw,
    DateOnly? DateIn,
    DateOnly? DateOut,
    bool? IsEffective,
    bool IncludedByPolicy,
    string? BomConditionId,
    string SourceTable);

public sealed record AttributeCountsDto(int Go, int Highlight, int Review);

public sealed record ItemClassAttributeDto(
    string AttributeId,
    string ValueSource,
    string AttributeSource,
    string? GlobalVariable,
    string? ValueListId,
    long? ValueListSize,
    string? ValueType,
    long NUsed,
    long? Covered,
    string Status,
    string? InheritedFrom,
    string? Note);

/// <summary>Class model of one item (latest completed analysis).</summary>
public sealed record ItemClassDto(
    string ItemId,
    string ClassId,
    string ParentClassId,
    string? Description,
    string ItemStatus,
    string? Readiness,
    string? ResolvedType,
    bool TypeChanged,
    AttributeCountsDto Counts,
    IReadOnlyList<ItemClassAttributeDto> Attributes,
    string RunId);

public sealed record MissingValuesDto(
    string ItemId,
    string AttributeId,
    string? GlobalVariable,
    string? ValueListId,
    string? ValueType,
    bool IsList,
    IReadOnlyList<string> Values,
    string RunId);

public sealed record IssueGroupDto(
    Guid Id,
    string GroupKey,
    string IssueType,
    Severity Severity,
    FixSource FixSource,
    string FixTarget,
    string FixAction,
    int AffectedItems,
    int AffectedBoms,
    string FirstSeenRunId,
    string LastSeenRunId,
    string? ResolvedInRunId,
    IssueStatus Status,
    string? AcknowledgedByUserId,
    DateTime? AcknowledgedAt,
    string? Note);

public sealed record IssueItemDto(
    string? ItemId,
    string? ParentItemId,
    string? LegacyFeatureId,
    string? LegacyValue,
    string? AttributeId,
    string? ValueCode,
    string? GlobalVariable,
    string? ValueListId,
    string? Detail);

public sealed record IssueSummaryDto(FixSource FixSource, Severity Severity, int Groups, int AffectedItems);

public sealed record AcknowledgeIssueRequestDto(string? Note);

public sealed record TypeChangeDto(
    string ItemId,
    string? Description,
    string? SwingTypeCode,
    string SourceType,
    string ResolvedType,
    string InheritedAttrs,
    long AffectedBoms,
    TypeDecisionStatus Decision,
    string? DecidedByUserId,
    DateTime? DecidedAt,
    string? Note);

public sealed record TypeDecisionRequestDto(IReadOnlyList<string> ItemIds, TypeDecisionStatus Decision, string? Note);

public sealed record TypeDecisionResultDto(int Updated, IReadOnlyList<string> NotFound);

public sealed record BomReadinessDto(
    string TopItemId,
    string? Description,
    long Items,
    long BlockedItems,
    long ReviewItems,
    long DecisionItems,
    long AlreadyExportedItems,
    long NotImportedItems,
    string Status);

public sealed record BomTreeNodeDto(string ItemId, string? Description, string Status, string? ResolvedType, bool TypeChanged);

public sealed record BomTreeEdgeDto(string ParentItemId, string ChildItemId);

public sealed record BomTreeDto(string TopItemId, int TotalItems, bool Truncated, IReadOnlyList<BomTreeNodeDto> Nodes, IReadOnlyList<BomTreeEdgeDto> Edges, string RunId);

public sealed record CreateWaveRequestDto(string Name, IReadOnlyList<string> TopBoms, DateOnly? EffectiveDate);

public sealed record WaveDto(
    string WaveId,
    string Name,
    IReadOnlyList<string> TopBoms,
    DateOnly EffectiveDate,
    int Items,
    WaveStatus Status,
    int BronzeVersion,
    int ReferenceVersion,
    string CreatedByUserId,
    DateTime CreatedAt,
    string? LastExportRunId);

public sealed record WaveGateDto(
    string WaveId,
    int Items,
    int AlreadyExportedReferenced,
    IReadOnlyList<Core.Rules.WaveGateCheck> Checks,
    bool CanExport,
    string AnalysisRunId,
    int? LatestBronzeVersion,
    int? LatestReferenceVersion);

public sealed record WaveFileDto(string Name, long Size, string RunId);

public sealed record BomReadinessCountsDto(int Ready, int Decision, int Review, int Blocked, int Exported);

/// <summary>Overview numbers from the latest completed analysis plus SQLite issue/decision state.</summary>
public sealed record AnalysisSummaryDto(
    string RunId,
    long Items,
    BomReadinessCountsDto Boms,
    int OpenIssueGroups,
    int PendingTypeDecisions,
    IReadOnlyList<IssueGroupDto> TopBlockingGroups);
