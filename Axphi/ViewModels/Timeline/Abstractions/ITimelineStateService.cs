using Axphi.Data;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public interface ITimelineStateService
{
    string SerializeSnapshot(Chart chart, ProjectMetadata metadata);

    (Chart Chart, ProjectMetadata Metadata) DeserializeSnapshot(string snapshot);

    Project RestoreProjectFromSnapshot(string snapshot, Project currentProject);

    TimelineUiState CaptureUiState(TimelineCaptureRuntime runtime);

    void RestoreUiState(
        TimelineUiState preservedState,
        ObservableCollection<TrackViewModel> tracks,
        AudioTrackViewModel? audioTrack,
        JudgementLineEditorViewModel judgementLineEditor);

    TimelinePlaybackRestoreState ResolvePlaybackState(TimelineUiState? preservedUiState, ProjectMetadata metadata);
}
