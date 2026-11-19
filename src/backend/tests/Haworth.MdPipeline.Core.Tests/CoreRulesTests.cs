using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Rules;

namespace Haworth.MdPipeline.Core.Tests;

public sealed class SqlGuardTests
{
    [Theory]
    [InlineData("SELECT ARANR FROM SWLIB.GSAR00")]
    [InlineData("  select STANR, STANRU from SWLIB.GSST00 where STBKAR = '1'")]
    public void AcceptsSingleSelect(string sql) => SqlGuard.EnsureSelectOnly(sql);

    [Theory]
    [InlineData("DELETE FROM SWLIB.GSST00")]
    [InlineData("SELECT * FROM SWLIB.GSST00; DELETE FROM SWLIB.GSST00")]
    [InlineData("SELECT * INTO copy FROM SWLIB.GSST00")]
    [InlineData("SELECTX FROM t")]
    [InlineData("SELECT 1 -- comment")]
    [InlineData("")]
    public void RejectsAnythingElse(string sql) =>
        Assert.Throws<DomainValidationException>(() => SqlGuard.EnsureSelectOnly(sql));

    [Fact]
    public void RendersOnlyAllowlistedLibraryIdentifiers()
    {
        Assert.Equal("SELECT A FROM SWLIB.GSAR00", SqlGuard.RenderLibrary("SELECT A FROM {library}.GSAR00", "SWLIB", ["swlib"]));
        Assert.Throws<DomainValidationException>(() => SqlGuard.RenderLibrary("SELECT A FROM {library}.GSAR00", "OTHER", ["SWLIB"]));
        Assert.Throws<DomainValidationException>(() => SqlGuard.RenderLibrary("SELECT A FROM {library}.GSAR00", "X.Y", ["X.Y"]));
    }
}

public sealed class SwingInterpretationTests
{
    private static SwingInterpretation Create(
        Dictionary<string, string>? codes = null, Dictionary<string, bool>? pseudo = null, string[]? openIn = null, int? pivot = null) =>
        SwingInterpretation.Create(
            codes ?? new() { ["0"] = "FIXED", ["2"] = "CONFIGURABLE" },
            pseudo ?? new() { ["Y"] = true, ["N"] = false },
            openIn ?? [], [], pivot, dateOutInclusive: true);

    [Fact]
    public void MapsConfirmedTypeCodes() =>
        Assert.Equal([new TypeCodeEntry(0, SourceType.Fixed), new TypeCodeEntry(2, SourceType.Configurable)], Create().TypeCodes);

    [Fact]
    public void RejectsTypeCodesThatWouldDefaultUnknownValues()
    {
        Assert.Throws<DomainValidationException>(() => Create(codes: new() { ["1"] = "UNKNOWN" }));
        Assert.Throws<DomainValidationException>(() => Create(codes: new() { ["x"] = "FIXED" }));
    }

    [Fact]
    public void RejectsBlankPseudoKeysSoBlankFlagsStayUnresolved() =>
        Assert.Throws<DomainValidationException>(() => Create(pseudo: new() { [" "] = false }));

    [Fact]
    public void ParsesOpenEndedPatternsAndValidatesPivot()
    {
        Assert.Equal([new OpenEndedDatePattern(9999, 12, 31)], Create(openIn: ["9999/12/31"]).OpenEndedDateIn);
        Assert.Throws<DomainValidationException>(() => Create(openIn: ["9999-12-31"]));
        Assert.Throws<DomainValidationException>(() => Create(pivot: 100));
    }
}

public sealed class BomTypePolicyTests
{
    [Fact]
    public void RoundTripsCanonicalJson()
    {
        var policy = BomTypePolicy.Create(BomTypePolicyMode.IncludeOnly, [" 2", "1", "2"]);

        Assert.Equal(["1", "2"], policy.Types);
        Assert.Equal(policy, BomTypePolicy.FromJson(policy.ToJson()));
    }

    [Fact]
    public void RejectsInconsistentPolicies()
    {
        Assert.Throws<DomainValidationException>(() => BomTypePolicy.Create(BomTypePolicyMode.IncludeOnly, []));
        Assert.Throws<DomainValidationException>(() => BomTypePolicy.Create(BomTypePolicyMode.AllTypesUnfiltered, ["1"]));
    }
}

public sealed class RunTransitionsTests
{
    [Theory]
    [InlineData(RunStatus.Queued, RunStatus.Syncing, true)]
    [InlineData(RunStatus.Syncing, RunStatus.Running, true)]
    [InlineData(RunStatus.Running, RunStatus.Completed, true)]
    [InlineData(RunStatus.Completed, RunStatus.Running, false)]
    [InlineData(RunStatus.Queued, RunStatus.Completed, false)]
    public void FollowsTheRunStateMachine(RunStatus from, RunStatus to, bool allowed) =>
        Assert.Equal(allowed, RunTransitions.CanTransition(from, to));
}
