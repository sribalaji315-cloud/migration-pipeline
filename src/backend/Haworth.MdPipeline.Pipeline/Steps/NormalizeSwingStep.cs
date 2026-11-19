using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>Step 2c: GSAR00/GSST00 Bronze → validated Swing items, BOM lines, effective edges and source issues.</summary>
public sealed class NormalizeSwingStep(IOptions<PipelineOptions> options) : IPipelineStep
{
    public string Name => nameof(NormalizeSwingStep);

    public IReadOnlyList<string> OutputTables =>
    [
        "swing_item", "swing_item_type", "bom_line", "effective_bom_line", "bom_edge", "bom_edge_line", "bom_parent",
        "bom_line_key_profile", "bom_cycle_edge", "swing_source_issue"
    ];

    public Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ctx.SilverPath))!);
        using var duck = DuckDbExecutor.Open(ctx.SilverPath, options.Value);

        duck.ExecuteFile("02_swing_cfg.sql");
        WriteConfig(duck, ctx);
        ct.ThrowIfCancellationRequested();

        duck.ExecuteFile("02_swing.sql", new Dictionary<string, string> { ["bronze_dir"] = SqlScriptLoader.SqlPath(ctx.RequireBronzeDir()) });
        ct.ThrowIfCancellationRequested();

        DetectCycles(duck, options.Value.MaxBomDepth, ct);
        return Task.FromResult(new StepResult(duck.CountRows("bom_line")));
    }

    private void WriteConfig(DuckDbExecutor duck, PipelineContext ctx)
    {
        var swing = ctx.Swing;
        duck.Append("cfg_run",
        [
            [ctx.RunId, ctx.EffectiveDate, swing.DateOutInclusive, swing.TwoDigitYearPivot, ctx.BomTypePolicy.Mode.ToString(), ctx.BomTypePolicy.ToJson()]
        ]);
        duck.Append("cfg_type_code", swing.TypeCodes.Select(t => new object?[]
        {
            t.Code, t.Type == SourceType.Fixed ? "FIXED" : "CONFIGURABLE"
        }));
        duck.Append("cfg_pseudo_flag", swing.PseudoFlags.Select(p => new object?[] { p.Raw, p.IsPseudo }));
        duck.Append("cfg_open_date",
            swing.OpenEndedDateIn.Select(p => new object?[] { "IN", p.Year, p.Month, p.Day })
                .Concat(swing.OpenEndedDateOut.Select(p => new object?[] { "OUT", p.Year, p.Month, p.Day })));
        duck.Append("cfg_bom_type", ctx.BomTypePolicy.Types.Select(t => new object?[] { t }));
        var rules = options.Value;
        duck.Append("cfg_rules", [[rules.ListRule, rules.LegacyReachableNullIncluded, rules.ReviewBlocksExport]]);
    }

    /// <summary>Iterative peel loop (set-based SQL per iteration). Non-convergence fails the run visibly.</summary>
    private static void DetectCycles(DuckDbExecutor duck, int maxBomDepth, CancellationToken ct)
    {
        duck.ExecuteFile("02b_bom_cycle_init.sql");
        var remaining = duck.CountRows("bom_cycle_work");
        var maxIterations = Math.Max(1, maxBomDepth) * 2 + 2;
        for (var i = 0; ; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i >= maxIterations)
            {
                throw new InvalidOperationException(
                    $"BOM cycle detection did not converge within {maxIterations} iterations (Pipeline:MaxBomDepth={maxBomDepth}).");
            }

            duck.ExecuteFile("02c_bom_cycle_peel.sql");
            var next = duck.CountRows("bom_cycle_work");
            if (next == remaining)
            {
                break;
            }

            remaining = next;
        }

        duck.ExecuteFile("02d_bom_cycle_finish.sql");
    }
}
