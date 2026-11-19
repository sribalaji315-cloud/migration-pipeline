using Haworth.MdPipeline.Core.Rules;

namespace Haworth.MdPipeline.Core.Tests;

public sealed class WaveGateTests
{
    [Fact]
    public void ExportsOnlyWhenEveryCheckIsZero()
    {
        var clean = WaveGate.Evaluate(new WaveGateInputs(0, 0, 0, 0, 0, 0));
        var stale = WaveGate.Evaluate(new WaveGateInputs(0, 0, 0, 0, 0, 1));

        Assert.True(WaveGate.CanExport(clean));
        Assert.False(WaveGate.CanExport(stale));
        Assert.Equal([WaveGate.SourceUnchanged], stale.Where(c => !c.Passed).Select(c => c.Name));
    }
}
