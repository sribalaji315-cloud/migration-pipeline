using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Sources;
using Haworth.MdPipeline.Pipeline.Steps;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Haworth.MdPipeline.Pipeline.Tests;

public sealed class TestWorkspace : IDisposable
{
    public static readonly DateOnly EffectiveDate = new(2026, 11, 19);

    public TestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "mdp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Options = new PipelineOptions
        {
            DataDir = Root,
            FixturesDir = Path.Combine(AppContext.BaseDirectory, "fixtures"),
            DuckDbMemoryLimit = "1GB",
            DuckDbThreads = 2,
            MappingBuckets = 4
        };
    }

    public string Root { get; }
    public PipelineOptions Options { get; }

    public BronzeParquetWriter Writer => new(Options);

    public static SwingOptions DefaultSwing() => new();

    public PipelineContext Context(string bronzeDir, SwingOptions? swing = null, BomTypePolicy? policy = null, BronzeTarget? target = null) =>
        new()
        {
            RunId = "R-" + Guid.NewGuid().ToString("N")[..8],
            EffectiveDate = EffectiveDate,
            BomTypePolicy = policy ?? BomTypePolicy.AllTypesUnfiltered,
            Swing = (swing ?? DefaultSwing()).ToInterpretation(),
            SilverPath = Path.Combine(Root, "runs", Guid.NewGuid().ToString("N"), "silver.duckdb"),
            BronzeTarget = target,
            BronzeDir = bronzeDir
        };

    /// <summary>Writes the Swing fixtures to a Bronze folder and returns it.</summary>
    public async Task<string> SyncSwingAsync(int version = 1)
    {
        var dir = Path.Combine(Root, "bronze", $"v{version}");
        Directory.CreateDirectory(dir);
        var target = new BronzeTarget(version, dir, SyncMode.Full, null, null);
        var ctx = Context(dir, target: target);
        var reader = new FixtureSwingReader(MsOptions.Create(Options));
        await new SyncSwingBomStep(reader, Writer).ExecuteAsync(ctx, CancellationToken.None);
        await new SyncSwingItemStep(reader, Writer).ExecuteAsync(ctx, CancellationToken.None);
        return dir;
    }

    public DuckDbExecutor OpenSilver(PipelineContext ctx) => DuckDbExecutor.Open(ctx.SilverPath, Options);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
