using Axphi.Data;

namespace Axphi.ViewModels;

public sealed class TimelinePlaybackRestoreService : ITimelinePlaybackRestoreService
{
    public TimelinePlaybackRestoreState Resolve(TimelineUiState? preservedUiState, ProjectMetadata metadata)
    {
        if (preservedUiState != null)
        {
            return new TimelinePlaybackRestoreState(
                preservedUiState.CurrentHorizontalScrollOffset,
                preservedUiState.WorkspaceStartTick,
                preservedUiState.WorkspaceEndTick,
                preservedUiState.CurrentPlayTimeSeconds,
                true);
        }

        return new TimelinePlaybackRestoreState(
            metadata.CurrentHorizontalScrollOffset,
            metadata.WorkspaceStartTick,
            metadata.WorkspaceEndTick,
            metadata.PlayheadTimeSeconds,
            true);
    }
}
