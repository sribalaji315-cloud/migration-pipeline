using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Sources;
using Haworth.MdPipeline.Pipeline.Steps;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Haworth.MdPipeline.Pipeline.Tests;

/// <summary>Runs steps 2c-12 on tests/fixtures/scenario (Swing + Migration Tool + Bluestar) and asserts the business rules.</summary>
public sealed class AnalysisScenarioTests : IDisposable
{
    private readonly TestWorkspace ws = new();

    public void Dispose() => ws.Dispose();

    private string ScenarioDir => Path.Combine(AppContext.BaseDirectory, "fixtures", "scenario");

    private async Task<DuckDbExecutor> AnalyseAsync(IReadOnlyList<TypeDecisionInput>? decisions = null)
    {
        var options = ws.Options;
        options.FixturesDir = ScenarioDir;
        var bronze = Path.Combine(ws.Root, "bronze", "v1");
        Directory.CreateDirectory(bronze);
        var ctx = ws.Context(bronze, target: new BronzeTarget(1, bronze, SyncMode.Full, null, null));
        var reader = new FixtureSwingReader(MsOptions.Create(options));
        await new SyncSwingBomStep(reader, ws.Writer).ExecuteAsync(ctx, CancellationToken.None);
        await new SyncSwingItemStep(reader, ws.Writer).ExecuteAsync(ctx, CancellationToken.None);
        await new SyncMigrationToolStep(new FixtureMigrationToolClient(MsOptions.Create(options)), ws.Writer,
            MsOptions.Create(options), MsOptions.Create(new MigrationToolOptions())).ExecuteAsync(ctx, CancellationToken.None);

        var reference = Path.Combine(ws.Root, "reference", "bs-1");
        Directory.CreateDirectory(reference);
        foreach (var csv in Directory.GetFiles(Path.Combine(ScenarioDir, "bluestar"), "*.csv"))
        {
            var columns = (await File.ReadAllLinesAsync(csv))[0].Split(',');
            await ws.Writer.WriteAsync(
                new BronzeTableSpec(Path.GetFileNameWithoutExtension(csv), columns.Select(c => new BronzeColumn(c)).ToList()),
                BronzeParquetWriter.FromExtract(new Core.Interfaces.SourceExtract("csv", columns, CsvTable.ReadAsync(csv, columns, CancellationToken.None)), CancellationToken.None),
                new BronzeMeta(1, DateTime.UtcNow, "Bluestar", Path.GetFileName(csv)),
                Path.Combine(reference, Path.GetFileNameWithoutExtension(csv) + ".parquet"), null, CancellationToken.None);
        }

        ctx.ReferenceDir = reference;
        ctx.TypeDecisions = decisions ?? [];
        var o = MsOptions.Create(options);
        IPipelineStep[] steps =
        [
            new NormalizeSwingStep(o), new ReferenceStep(o), new MappingStep(o), new ClassAttributeStep(o), new GlobalVariableStep(o),
            new InheritanceStep(o), new ItemTypeStep(o), new FinalModelStep(o), new BomStep(o), new IssuesStep(o), new ReadinessStep(o)
        ];
        foreach (var step in steps)
        {
            await step.ExecuteAsync(ctx, CancellationToken.None);
        }

        return ws.OpenSilver(ctx);
    }

    private static List<string> Rows(DuckDbExecutor duck, string sql) =>
        duck.Query(sql).Select(r => string.Join("|", r.Values.Select(v => v?.ToString() ?? "∅"))).ToList();

    [Fact]
    public async Task AppliesMappingExclusionRules()
    {
        using var duck = await AnalyseAsync();

        Assert.Equal(["MID|IGNORED", "TOP|MAPPING_MISSING", "TOP|NOT_ENGINEERING", "TOP|NOT_REQUIRED"],
            Rows(duck, "SELECT item_id, reason FROM map_rules_excluded ORDER BY item_id, reason"));
        Assert.Equal(["MID|ColorOfFabric|X9"], Rows(duck, "SELECT item_id, target_attribute, target_value FROM map_rules_clean WHERE load_status = 'PENDING_REVIEW'"));
        Assert.Equal(8L, duck.CountRows("mt_scope_item"));
    }

    [Fact]
    public async Task AssignsGlobalVariablesByCoverageAndUsage()
    {
        using var duck = await AnalyseAsync();

        Assert.Equal(
        [
            "CLEAN|ColorLevel3|Global_Color|Global_Color|1|1|GO",
            "CLEANLEAF|Width|Global_Width|∅|1|1|GO",
            "LEAF|Width|Global_Width|∅|2|1|HIGHLIGHT",
            "LEAF2|Height|∅|∅|1|1|GO",
            "MID|ColorOfFabric|Global_UpholsteredColors|Global_UpholsteredColors|1|1|GO",
            "ORPHAN|BackFabric|Global_Color|Global_Color|1|1|GO",
            "TOP|ColorLevel3|Global_Color|Global_Color|3|3|GO",
            "TOP|Height|∅|∅|1|1|GO",
            "TOP2|ColorLevel3|Global_Color|Global_Color|1|0|HIGHLIGHT",
        ], Rows(duck, "SELECT item_id, attribute_id, global_variable, value_list_id, n_used, covered, status FROM attr_gv_assigned ORDER BY ALL"));

        // Upholstered (2 classes, incl. one where only the global variable says VALUE_LIST) beats Felt (1 class).
        Assert.Equal(["Global_UpholsteredColors|2", "Global_FeltColors|1"],
            Rows(duck, "SELECT global_variable, class_usage FROM bs_gv_candidate WHERE variable_name = 'ColorOfFabric' ORDER BY class_usage DESC"));
        Assert.Equal(["LEAF|Width|abc", "TOP2|ColorLevel3|F9999"], Rows(duck, "SELECT item_id, attribute_id, value_code FROM attr_missing_values ORDER BY ALL"));
        Assert.Equal(["TOP|LevelCombination|REVIEW|MAPPING_API"],
            Rows(duck, "SELECT item_id, attribute_id, status, attribute_source FROM item_class_attribute WHERE status = 'REVIEW' AND value_source = 'LOCAL'"));
    }

    [Fact]
    public async Task InheritsConditionNeedsFromDirectParentsAndResolvesTypes()
    {
        using var duck = await AnalyseAsync();

        Assert.Equal(
        [
            "CLEAN|CLEANLEAF|ColorLevel3|CLEAN|1|False",
            "MID|LEAF|ColorLevel3|TOP|1|True",
            "TOP|MID|ColorLevel3|TOP|2|False",
        ], Rows(duck, "SELECT parent_id, child_id, attribute_id, owner_id, level, is_pseudo_line FROM inheritance_link_s ORDER BY ALL"));
        Assert.Equal(["TOP2|BackFabric|1"], Rows(duck, "SELECT top_item_id, attribute_id, level FROM top_missing"));
        Assert.Equal(["CLEANLEAF|ColorLevel3|Global_Color|CLEAN|GO", "LEAF|ColorLevel3|Global_Color|TOP|GO", "MID|ColorLevel3|Global_Color|TOP|GO"],
            Rows(duck, "SELECT item_id, attribute_id, global_variable, inherited_from, status FROM item_class_attribute WHERE value_source = 'INHERITED' AND inherited_from <> '' ORDER BY ALL"));
        Assert.Equal(["CLEANLEAF|ColorLevel3", "LEAF|ColorLevel3", "LEAF2|BackFabric", "MID|ColorLevel3"],
            Rows(duck, "SELECT item_id, inherited_attrs FROM item_type_resolved WHERE type_changed ORDER BY item_id"));
        Assert.Equal("CONFIGURABLE", duck.Scalar("SELECT resolved_type FROM item_type_resolved WHERE item_id = 'TOP2'"));
    }

    [Fact]
    public async Task ReportsIssuesAndReadiness()
    {
        using var duck = await AnalyseAsync();

        Assert.Equal(
        [
            "ATTRIBUTE_NOT_IN_BS|Review|1", "FIXED_TO_CONFIGURABLE|Approve|4", "INHERITED_ATTRIBUTE_MISSING_ON_TOP_ITEM|Block|1",
            "ITEM_NOT_APPROVED_FOR_MIGRATION|Block|1", "MAPPING_MISSING|Review|1", "MAPPING_PENDING_REVIEW|Review|1",
            "VALUES_INVALID_FOR_TYPE|Block|1", "VALUES_NOT_IN_VALUELIST|Block|1"
        ], Rows(duck, "SELECT issue_type, min(severity), count(*) FROM review_issues GROUP BY issue_type ORDER BY issue_type"));
        Assert.Equal(["ORPHAN|1|READY", "CLEAN|2|DECISION", "TOP|3|BLOCKED", "TOP2|2|BLOCKED"],
            Rows(duck, "SELECT top_item_id, items, status FROM bom_readiness ORDER BY status DESC, top_item_id"));
        Assert.Equal(1L, Convert.ToInt64(duck.Scalar("SELECT affected_boms FROM review_issue_groups WHERE issue_type = 'VALUES_NOT_IN_VALUELIST'")));
    }

    [Fact]
    public async Task ApprovedTypeDecisionMakesBomReadyOnlyWhileItStillMatches()
    {
        using (var duck = await AnalyseAsync([new TypeDecisionInput("CLEANLEAF", "Approved", "ColorLevel3", "0")]))
        {
            Assert.Equal("READY", duck.Scalar("SELECT status FROM bom_readiness WHERE top_item_id = 'CLEAN'"));
        }

        ws.Dispose();
    }

    [Fact]
    public async Task StaleTypeDecisionStaysPending()
    {
        using var duck = await AnalyseAsync([new TypeDecisionInput("CLEANLEAF", "Approved", "Height", "0")]);

        Assert.Equal("DECISION", duck.Scalar("SELECT status FROM bom_readiness WHERE top_item_id = 'CLEAN'"));
    }
}
