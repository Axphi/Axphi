using Axphi.Data;

namespace Axphi.ViewModels;

public interface ITimelineWorkspaceLoopService
{
    double? ResolveLoopTargetSeconds(
        Chart chart,
        int totalDurationTicks,
        int workspaceStartTick,
        int workspaceEndTick,
        double previousTimeSeconds,
        double currentTimeSeconds);
}
