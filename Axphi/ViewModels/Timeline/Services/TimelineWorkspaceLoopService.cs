using Axphi.Data;
using Axphi.Utilities;

namespace Axphi.ViewModels;

public sealed class TimelineWorkspaceLoopService : ITimelineWorkspaceLoopService
{
    public double? ResolveLoopTargetSeconds(
        Chart chart,
        int totalDurationTicks,
        int workspaceStartTick,
        int workspaceEndTick,
        double previousTimeSeconds,
        double currentTimeSeconds)
    {
        double totalSeconds = TimeTickConverter.TickToTime(totalDurationTicks, chart.BpmKeyFrames, chart.InitialBpm);
        double workspaceEndSeconds = TimeTickConverter.TickToTime(workspaceEndTick, chart.BpmKeyFrames, chart.InitialBpm);

        bool shouldLoop = false;

        if (workspaceStartTick < workspaceEndTick
            && previousTimeSeconds < workspaceEndSeconds
            && currentTimeSeconds >= workspaceEndSeconds)
        {
            shouldLoop = true;
        }
        else if (currentTimeSeconds >= totalSeconds)
        {
            shouldLoop = true;
        }

        if (!shouldLoop)
        {
            return null;
        }

        return TimeTickConverter.TickToTime(workspaceStartTick, chart.BpmKeyFrames, chart.InitialBpm);
    }
}
