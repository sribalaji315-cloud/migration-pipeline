namespace Haworth.MdPipeline.Core.Rules;

/// <summary>Counts measured over the union of a wave's BOM explosions (Silver of the latest analysis + SQLite decisions).</summary>
public sealed record WaveGateInputs(
    int IssueItems,
    int NotImportedItems,
    int TypeChangesNotApproved,
    int UnknownTypes,
    int MissingReferences,
    int SourceChanges);

public sealed record WaveGateCheck(string Name, bool Passed, int Count);

/// <summary>Export is all-or-nothing: every check must pass (count 0).</summary>
public static class WaveGate
{
    public const string NoBlockingOrReviewIssues = nameof(NoBlockingOrReviewIssues);
    public const string ItemsImportedFromMigrationTool = nameof(ItemsImportedFromMigrationTool);
    public const string TypeChangesApproved = nameof(TypeChangesApproved);
    public const string ItemTypesKnown = nameof(ItemTypesKnown);
    public const string ReferencesExistInBluestar = nameof(ReferencesExistInBluestar);
    public const string SourceUnchanged = nameof(SourceUnchanged);

    public static IReadOnlyList<WaveGateCheck> Evaluate(WaveGateInputs inputs) =>
    [
        Check(NoBlockingOrReviewIssues, inputs.IssueItems),
        Check(ItemsImportedFromMigrationTool, inputs.NotImportedItems),
        Check(TypeChangesApproved, inputs.TypeChangesNotApproved),
        Check(ItemTypesKnown, inputs.UnknownTypes),
        Check(ReferencesExistInBluestar, inputs.MissingReferences),
        Check(SourceUnchanged, inputs.SourceChanges)
    ];

    public static bool CanExport(IReadOnlyList<WaveGateCheck> checks) => checks.All(c => c.Passed);

    private static WaveGateCheck Check(string name, int count) => new(name, count == 0, count);
}
