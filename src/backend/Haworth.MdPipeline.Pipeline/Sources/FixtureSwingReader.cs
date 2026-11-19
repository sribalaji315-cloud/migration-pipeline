using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>Used when Swing:Enabled=false. Reads {FixturesDir}/swing/GSST00.csv and GSAR00.csv.</summary>
public sealed class FixtureSwingReader(IOptions<PipelineOptions> options) : ISwingBomReader, ISwingItemReader, ISwingHealthProbe
{
    private readonly string dir = Path.Combine(options.Value.FixturesDir, "swing");

    public SourceExtract ReadBom(CancellationToken ct) =>
        new(SwingContract.BomTable, SwingContract.Gsst00Columns,
            CsvTable.ReadAsync(Path.Combine(dir, "GSST00.csv"), SwingContract.Gsst00Columns, ct));

    public SourceExtract ReadItems(CancellationToken ct) =>
        new(SwingContract.ItemTable, SwingContract.Gsar00Columns,
            CsvTable.ReadAsync(Path.Combine(dir, "GSAR00.csv"), SwingContract.Gsar00Columns, ct));

    public Task<SourceHealth> CheckAsync(CancellationToken ct)
    {
        var ok = File.Exists(Path.Combine(dir, "GSST00.csv")) && File.Exists(Path.Combine(dir, "GSAR00.csv"));
        return Task.FromResult(new SourceHealth("Swing", ok, ok ? $"Fixture mode ({Path.GetFullPath(dir)})." : $"Fixture files missing in {Path.GetFullPath(dir)}."));
    }
}
