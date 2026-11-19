using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Duck;

namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>Step 2a: GSST00 → Bronze swing_bom_raw (authoritative BOM, full extract).</summary>
public sealed class SyncSwingBomStep(ISwingBomReader reader, BronzeParquetWriter writer) : IPipelineStep
{
    public string Name => nameof(SyncSwingBomStep);
    public IReadOnlyList<string> OutputTables => ["swing_bom_raw"];

    public async Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var target = ctx.BronzeTarget ?? throw new InvalidOperationException("Sync step requires a Bronze target.");
        var extractedAt = DateTime.UtcNow;
        var extract = reader.ReadBom(ct);
        var result = await writer.WriteAsync(
            BronzeTableSpec.FromExtract("swing_bom_raw", extract),
            BronzeParquetWriter.FromExtract(extract, ct),
            new BronzeMeta(target.Version, extractedAt, "Swing", extract.SourceTable),
            Path.Combine(target.StagingDir, "swing_bom_raw.parquet"),
            partitionBy: null,
            ct);
        ctx.Sync.SwingBom = result;
        return new StepResult(result.Rows);
    }
}

/// <summary>Step 2b: GSAR00 → Bronze swing_item_raw (item master, full extract).</summary>
public sealed class SyncSwingItemStep(ISwingItemReader reader, BronzeParquetWriter writer) : IPipelineStep
{
    public string Name => nameof(SyncSwingItemStep);
    public IReadOnlyList<string> OutputTables => ["swing_item_raw"];

    public async Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var target = ctx.BronzeTarget ?? throw new InvalidOperationException("Sync step requires a Bronze target.");
        var extractedAt = DateTime.UtcNow;
        var extract = reader.ReadItems(ct);
        var result = await writer.WriteAsync(
            BronzeTableSpec.FromExtract("swing_item_raw", extract),
            BronzeParquetWriter.FromExtract(extract, ct),
            new BronzeMeta(target.Version, extractedAt, "Swing", extract.SourceTable),
            Path.Combine(target.StagingDir, "swing_item_raw.parquet"),
            partitionBy: null,
            ct);
        ctx.Sync.SwingItem = result;
        return new StepResult(result.Rows);
    }
}
