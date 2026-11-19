using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>Type decision state from SQLite, compared in SQL against the current inherited attributes and Swing code.</summary>
public sealed record TypeDecisionInput(string ItemId, string Decision, string InheritedAttrs, string? SwingTypeCode);

/// <summary>Runs embedded SQL files against the run's Silver DuckDB; C# only orchestrates.</summary>
public abstract class SilverSqlStep(IOptions<PipelineOptions> options) : IPipelineStep
{
    protected PipelineOptions Options => options.Value;

    public abstract string Name { get; }
    public abstract IReadOnlyList<string> OutputTables { get; }

    /// <summary>Table whose row count is reported as RowsOut.</summary>
    protected abstract string CountTable { get; }

    public Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        using var duck = DuckDbExecutor.Open(ctx.SilverPath, options.Value);
        Run(duck, ctx, ct);
        return Task.FromResult(new StepResult(duck.CountRows(CountTable)));
    }

    protected abstract void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct);

    protected static Dictionary<string, string> Literal(string token, string path) => new() { [token] = SqlScriptLoader.SqlPath(path) };
}

public sealed class ReferenceStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(ReferenceStep);
    public override IReadOnlyList<string> OutputTables => ["bs_class", "bs_global_variable", "bs_vl_value", "bs_class_var", "bs_gv_candidate", "bs_variable"];
    protected override string CountTable => "bs_class_var";

    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) =>
        duck.ExecuteFile("03_reference.sql", Literal("reference_dir",
            ctx.ReferenceDir ?? throw new InvalidOperationException("No Bluestar reference version is pinned.")));
}

public sealed class MappingStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(MappingStep);
    public override IReadOnlyList<string> OutputTables => ["map_rules_clean", "map_rules_excluded", "mt_item", "mt_scope_item"];
    protected override string CountTable => "map_rules_clean";

    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct)
    {
        var bronze = ctx.RequireBronzeDir();
        duck.ExecuteFile("04a_mt_views.sql");
        var mappings = Path.Combine(bronze, "mappings");
        if (Directory.Exists(mappings) && Directory.EnumerateFiles(mappings, "*.parquet", SearchOption.AllDirectories).Any())
        {
            duck.ExecuteFile("04b_mt_mappings_view.sql", Literal("bronze_dir", bronze));
        }

        if (File.Exists(Path.Combine(bronze, "items.parquet")))
        {
            duck.ExecuteFile("04c_mt_items_view.sql", Literal("bronze_dir", bronze));
        }

        duck.ExecuteFile("04_mapping.sql");
    }
}

public sealed class ClassAttributeStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(ClassAttributeStep);
    public override IReadOnlyList<string> OutputTables => ["item_class", "item_attr", "item_used_value"];
    protected override string CountTable => "item_attr";
    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) => duck.ExecuteFile("05_class_attr.sql");
}

public sealed class GlobalVariableStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(GlobalVariableStep);
    public override IReadOnlyList<string> OutputTables => ["attr_gv_check", "attr_gv_assigned", "attr_missing_values"];
    protected override string CountTable => "attr_gv_assigned";
    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) => duck.ExecuteFile("06_gv_valuelist.sql");
}

/// <summary>R14/R15: pushes unowned condition needs one level at a time; the C# loop stops when no need moves further.</summary>
public sealed class InheritanceStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(InheritanceStep);
    public override IReadOnlyList<string> OutputTables => ["item_need", "inheritance_link_s", "inh_node", "inh_owner", "top_missing"];
    protected override string CountTable => "inheritance_link_s";

    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct)
    {
        duck.ExecuteFile("07a_inheritance_init.sql");
        var maxLevels = Math.Max(1, Options.MaxBomDepth);
        for (var level = 0; Convert.ToInt64(duck.Scalar(SqlScriptLoader.Load("07d_inheritance_frontier_count.sql"))) > 0; level++)
        {
            ct.ThrowIfCancellationRequested();
            if (level >= maxLevels)
            {
                throw new InvalidOperationException(
                    $"Inheritance did not finish within {maxLevels} BOM levels (Pipeline:MaxBomDepth). The scope is not truncated; fix the BOM or raise the limit.");
            }

            duck.ExecuteFile("07b_inheritance_level.sql");
        }

        duck.ExecuteFile("07c_inheritance_finish.sql");
    }
}

public sealed class ItemTypeStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(ItemTypeStep);
    public override IReadOnlyList<string> OutputTables => ["item_type_resolved"];
    protected override string CountTable => "item_type_resolved";
    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) => duck.ExecuteFile("08_item_type.sql");
}

public sealed class FinalModelStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(FinalModelStep);
    public override IReadOnlyList<string> OutputTables => ["item_class_attribute", "item_values_resolved", "item_value_condition"];
    protected override string CountTable => "item_class_attribute";
    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) => duck.ExecuteFile("09_final.sql");
}

public sealed class BomStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(BomStep);
    public override IReadOnlyList<string> OutputTables => ["top_bom", "bom_wave_items"];
    protected override string CountTable => "bom_wave_items";
    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) => duck.ExecuteFile("10_bom.sql");
}

public sealed class IssuesStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(IssuesStep);
    public override IReadOnlyList<string> OutputTables => ["review_issues", "review_issue_groups"];
    protected override string CountTable => "review_issues";
    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct) => duck.ExecuteFile("11_issues.sql");
}

public sealed class ReadinessStep(IOptions<PipelineOptions> options) : SilverSqlStep(options)
{
    public override string Name => nameof(ReadinessStep);
    public override IReadOnlyList<string> OutputTables => ["item_readiness", "bom_readiness"];
    protected override string CountTable => "item_readiness";

    protected override void Run(DuckDbExecutor duck, PipelineContext ctx, CancellationToken ct)
    {
        duck.ExecuteFile("12a_readiness_inputs.sql");
        duck.Append("cfg_type_decision", ctx.TypeDecisions.Select(d => new object?[] { d.ItemId, d.Decision, d.InheritedAttrs, d.SwingTypeCode }));
        duck.Append("cfg_exported_item", ctx.ExportedItems.Select(i => new object?[] { i }));
        duck.ExecuteFile("12_readiness.sql");
    }
}
