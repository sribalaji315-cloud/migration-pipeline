namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>
/// Migration Tool-only import: copies the Swing Bronze files (GSST00, GSAR00) of the previous published snapshot unchanged,
/// so Swing is not contacted. Runs before <see cref="SyncMigrationToolStep"/>, which scopes its item lookups to GSST00 items.
/// </summary>
public sealed class CarryForwardSwingStep : IPipelineStep
{
    private static readonly string[] Files = ["swing_bom_raw.parquet", "swing_item_raw.parquet"];

    public string Name => nameof(CarryForwardSwingStep);
    public IReadOnlyList<string> OutputTables => ["swing_bom_raw", "swing_item_raw"];

    public Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var target = ctx.BronzeTarget ?? throw new InvalidOperationException("Sync step requires a Bronze target.");
        if (target.PreviousDir is null || target.PreviousVersion is null)
        {
            throw new InvalidOperationException("No previous snapshot with Swing data exists. Import Swing data first.");
        }

        foreach (var file in Files)
        {
            var source = Path.Combine(target.PreviousDir, file);
            if (!File.Exists(source))
            {
                throw new InvalidOperationException($"Snapshot v{target.PreviousVersion} has no {file}. Import Swing data first.");
            }

            ct.ThrowIfCancellationRequested();
            File.Copy(source, Path.Combine(target.StagingDir, file));
        }

        ctx.Sync.SwingCarriedFromVersion = target.PreviousVersion;
        return Task.FromResult(new StepResult(0));
    }
}
