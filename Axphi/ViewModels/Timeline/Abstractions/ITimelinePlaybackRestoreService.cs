using Axphi.Data;

namespace Axphi.ViewModels;

public interface ITimelinePlaybackRestoreService
{
    TimelinePlaybackRestoreState Resolve(TimelineUiState? preservedUiState, ProjectMetadata metadata);
}
