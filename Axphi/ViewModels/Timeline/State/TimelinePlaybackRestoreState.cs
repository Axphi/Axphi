namespace Axphi.ViewModels;

public sealed record TimelinePlaybackRestoreState(
    double CurrentHorizontalScrollOffset,
    int WorkspaceStartTick,
    int WorkspaceEndTick,
    double CurrentPlayTimeSeconds,
    bool ShouldForceSeek);
