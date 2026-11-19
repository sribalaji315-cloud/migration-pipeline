using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>
/// Swing-only import: copies the Migration Tool Bronze files (mappings, items) of the previous published snapshot unchanged,
/// so the Migration Tool is not contacted. Copied rows keep their original _bronze_version as provenance.
/// </summary>
public sealed class CarryForwardMigrationToolStep(IOptions<PipelineOptions> options) : IPipelineStep
{
    public string Name => nameof(CarryForwardMigrationToolStep);
    public IReadOnlyList<string> OutputTables => ["mappings", "items"];

    public Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var target = ctx.BronzeTarget ?? throw new InvalidOperationException("Sync step requires a Bronze target.");
        ctx.Sync.EffectiveMode = target.Mode;
        if (target.PreviousDir is null || target.PreviousVersion is null)
        {
            return Task.FromResult(new StepResult(0));
        }

        var previousMappings = Path.Combine(target.PreviousDir, "mappings");
        var previousItems = Path.Combine(target.PreviousDir, "items.parquet");
        if (Directory.Exists(previousMappings))
        {
            CopyDirectory(previousMappings, Path.Combine(target.StagingDir, "mappings"), ct);
        }

        if (File.Exists(previousItems))
        {
            File.Copy(previousItems, Path.Combine(target.StagingDir, "items.parquet"));
        }

        ctx.Sync.MtCarriedFromVersion = target.PreviousVersion;
        var literals = new Dictionary<string, string> { ["bronze_dir"] = SqlScriptLoader.SqlPath(target.StagingDir) };
        using var duck = DuckDbExecutor.Open(null, options.Value);

        if (Directory.Exists(previousMappings) && Directory.EnumerateFiles(previousMappings, "*.parquet", SearchOption.AllDirectories).Any())
        {
            var summary = duck.Query(SqlScriptLoader.Load("01_mappings_summary.sql", literals)).Single();
            ctx.Sync.MappingRows = Convert.ToInt64(summary["mapping_rows"]);
            ctx.Sync.MaxUpdatedAt = summary["max_updated_at"] as DateTime?;
        }

        if (File.Exists(previousItems))
        {
            ctx.Sync.MtItems = Convert.ToInt32(duck.Query(SqlScriptLoader.Load("01_items_count.sql", literals)).Single()["item_rows"]);
        }

        return Task.FromResult(new StepResult(ctx.Sync.MappingRows + ctx.Sync.MtItems));
    }

    private static void CopyDirectory(string source, string destination, CancellationToken ct)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
