using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Rules;

public static class RunTransitions
{
    private static readonly HashSet<(RunStatus From, RunStatus To)> Allowed =
    [
        (RunStatus.Queued, RunStatus.Syncing),
        (RunStatus.Queued, RunStatus.Running),
        (RunStatus.Queued, RunStatus.Failed),
        (RunStatus.Syncing, RunStatus.Running),
        (RunStatus.Syncing, RunStatus.Failed),
        (RunStatus.Running, RunStatus.Completed),
        (RunStatus.Running, RunStatus.Failed)
    ];

    public static bool CanTransition(RunStatus from, RunStatus to) => Allowed.Contains((from, to));

    public static bool IsTerminal(RunStatus status) => status is RunStatus.Completed or RunStatus.Failed;
}
