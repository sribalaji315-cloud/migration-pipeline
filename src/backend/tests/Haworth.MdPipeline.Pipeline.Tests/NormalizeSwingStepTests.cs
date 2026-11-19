using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Steps;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Haworth.MdPipeline.Pipeline.Tests;

public sealed class NormalizeSwingStepTests : IDisposable
{
    private readonly TestWorkspace ws = new();

    public void Dispose() => ws.Dispose();

    private async Task<(PipelineContext Ctx, DuckDbExecutor Duck)> RunAsync(SwingOptions? swing = null, BomTypePolicy? policy = null)
    {
        var bronze = await ws.SyncSwingAsync();
        var ctx = ws.Context(bronze, swing, policy);
        await new NormalizeSwingStep(MsOptions.Create(ws.Options)).ExecuteAsync(ctx, CancellationToken.None);
        return (ctx, ws.OpenSilver(ctx));
    }

    private static List<string> Issues(DuckDbExecutor duck, string type) =>
        duck.Query($"SELECT coalesce(parent_item_id, '') || '>' || coalesce(item_id, '') AS k FROM swing_source_issue WHERE issue_type = '{type}' ORDER BY k")
            .Select(r => (string)r["k"]!).ToList();

    private static object? Scalar(DuckDbExecutor duck, string sql) => duck.Scalar(sql);

    [Fact]
    public async Task ResolvesItemTypesFromArvakzAndJoinsParentOnAranr()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        var types = duck.Query("SELECT item_id, source_type FROM swing_item_type")
            .ToDictionary(r => (string)r["item_id"]!, r => (string)r["source_type"]!);
        Assert.Equal("CONFIGURABLE", types["A5TBL01"]);
        Assert.Equal("FIXED", types["00123"]);
        Assert.Equal("CONFIGURABLE", types["PAD01"]);
        Assert.Equal("UNKNOWN", types["UNKN1"]);

        // STANR '00123' keeps its leading zeros and joins GSAR00.ARANR.
        Assert.Equal("FIXED", Scalar(duck,
            "SELECT t.source_type FROM bom_line l JOIN swing_item_type t ON t.item_id = l.parent_item_id WHERE l.parent_item_id = '00123'"));
    }

    [Fact]
    public async Task RaisesItemTypeUnknownForMissingOrUnsupportedEffectiveItemsOnly()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        Assert.Equal([">MISSING", ">UNKN1"], Issues(duck, "ITEM_TYPE_UNKNOWN"));
        Assert.Contains("No GSAR00 record", (string)Scalar(duck, "SELECT detail FROM swing_source_issue WHERE issue_type = 'ITEM_TYPE_UNKNOWN' AND item_id = 'MISSING'")!);
        Assert.Contains("'7'", (string)Scalar(duck, "SELECT detail FROM swing_source_issue WHERE issue_type = 'ITEM_TYPE_UNKNOWN' AND item_id = 'UNKN1'")!);
        Assert.Equal(0L, Convert.ToInt64(Scalar(duck, "SELECT count(*) FROM swing_source_issue WHERE item_id IN ('OLDCHILD', 'STARTSTOMORROW')")));
    }

    [Fact]
    public async Task FlagsAmbiguousItemModelsWithoutChoosingARow()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        Assert.Equal([">AMBIG"], Issues(duck, "ITEM_MODEL_AMBIGUOUS"));
        Assert.Equal("UNKNOWN", Scalar(duck, "SELECT source_type FROM swing_item_type WHERE item_id = 'AMBIG'"));
        Assert.Equal(0L, Convert.ToInt64(Scalar(duck, "SELECT count(*) FROM swing_source_issue WHERE issue_type = 'ITEM_TYPE_UNKNOWN' AND item_id = 'AMBIG'")));
    }

    [Fact]
    public async Task KeepsPseudoPerParentAndFlagsUnknownPseudoValues()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        var pseudo = duck.Query("SELECT parent_item_id, is_pseudo_line FROM bom_line WHERE child_item_id = 'PSEUDOCHILD'")
            .ToDictionary(r => (string)r["parent_item_id"]!, r => (bool)r["is_pseudo_line"]!);
        Assert.True(pseudo["A5TBL01"]);
        Assert.False(pseudo["00123"]);
        Assert.Equal(["A5TBL01>PAD01"], Issues(duck, "PSEUDO_FLAG_UNKNOWN"));
        Assert.Equal(0L, Convert.ToInt64(Scalar(duck, "SELECT count(*) FROM swing_source_issue WHERE item_id = 'PSEUDOCHILD'")));
    }

    [Fact]
    public async Task AppliesEffectivityBoundariesAndKeepsUnresolvedDatesInScope()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        var effective = duck.Query("SELECT DISTINCT child_item_id, is_effective FROM bom_line WHERE parent_item_id = 'A5TBL01'")
            .ToDictionary(r => (string)r["child_item_id"]!, r => r["is_effective"] as bool?);
        Assert.True(effective["ENDSTODAY"]);
        Assert.False(effective["STARTSTOMORROW"]);
        Assert.False(effective["OLDCHILD"]);
        Assert.Null(effective["ZERODATE"]);

        Assert.Equal(["A5TBL01>ZERODATE"], Issues(duck, "BOM_DATE_UNRESOLVED"));
        Assert.Equal(false, Scalar(duck, "SELECT effectivity_resolved FROM effective_bom_line WHERE child_item_id = 'ZERODATE'"));
        Assert.Equal(new DateOnly(2020, 1, 1), (DateOnly)Scalar(duck, "SELECT date_in FROM bom_line WHERE child_item_id = '00123'")!);
    }

    [Fact]
    public async Task PreservesRepeatedPositionsAndProfilesTheCandidateKey()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        Assert.Equal(2L, Convert.ToInt64(Scalar(duck, "SELECT count(DISTINCT bom_line_id) FROM bom_line WHERE parent_item_id = 'A5TBL01' AND bom_position = '0020'")));
        Assert.Equal(2L, Convert.ToInt64(Scalar(duck, "SELECT line_count FROM bom_edge WHERE parent_item_id = 'A5TBL01' AND child_item_id = 'A34009801'")));
        Assert.Equal(["A5TBL01>"], Issues(duck, "BOM_LINE_KEY_NOT_UNIQUE"));
    }

    [Fact]
    public async Task DetectsCyclesWithoutFlaggingEdgesIntoTheCycle()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        Assert.Equal(["CYC1>CYC2", "CYC2>CYC1"], Issues(duck, "BOM_CYCLE"));
    }

    [Fact]
    public async Task FlagsChildModelAndQuantityProblems()
    {
        var (_, duck) = await RunAsync();
        using var _d = duck;

        Assert.Equal(["A5TBL01>CHILDX"], Issues(duck, "BOM_CHILD_MODEL_NOT_FOUND"));
        Assert.Equal(["A5TBL01>CHILDX"], Issues(duck, "BOM_QUANTITY_UNPARSEABLE"));
    }

    [Fact]
    public async Task ConfiguredOpenEndedPatternResolvesZeroDates()
    {
        var swing = TestWorkspace.DefaultSwing();
        swing.OpenEndedDateIn = ["0/0/0"];
        var (_, duck) = await RunAsync(swing);
        using var _d = duck;

        Assert.Empty(Issues(duck, "BOM_DATE_UNRESOLVED"));
        Assert.Equal(true, Scalar(duck, "SELECT is_effective FROM bom_line WHERE child_item_id = 'ZERODATE'"));
    }

    [Fact]
    public async Task ExclusiveDateOutEndsLinesOnTheEffectiveDate()
    {
        var swing = TestWorkspace.DefaultSwing();
        swing.DateOutInclusive = false;
        var (_, duck) = await RunAsync(swing);
        using var _d = duck;

        Assert.Equal(false, Scalar(duck, "SELECT is_effective FROM bom_line WHERE child_item_id = 'ENDSTODAY'"));
    }

    [Fact]
    public async Task BomTypePolicyFiltersOnlyTheTraversalScope()
    {
        var (_, duck) = await RunAsync(policy: BomTypePolicy.Create(BomTypePolicyMode.IncludeOnly, ["1"]));
        using var _d = duck;

        Assert.Equal(false, Scalar(duck, "SELECT included_by_policy FROM bom_line WHERE child_item_id = 'UNKN1'"));
        Assert.Equal(0L, Convert.ToInt64(Scalar(duck, "SELECT count(*) FROM effective_bom_line WHERE child_item_id = 'UNKN1'")));
        Assert.DoesNotContain(">UNKN1", Issues(duck, "ITEM_TYPE_UNKNOWN"));
    }

    [Fact]
    public async Task ProducesIdenticalLineIdsForTheSamePinnedInputs()
    {
        var bronze = await ws.SyncSwingAsync();
        var step = new NormalizeSwingStep(MsOptions.Create(ws.Options));
        var ids = new List<string>[2];
        for (var i = 0; i < 2; i++)
        {
            var ctx = ws.Context(bronze);
            await step.ExecuteAsync(ctx, CancellationToken.None);
            using var duck = ws.OpenSilver(ctx);
            ids[i] = duck.Query("SELECT bom_line_id FROM bom_line ORDER BY bom_line_id").Select(r => (string)r["bom_line_id"]!).ToList();
        }

        Assert.Equal(17, ids[0].Count);
        Assert.Equal(ids[0], ids[1]);
    }
}
