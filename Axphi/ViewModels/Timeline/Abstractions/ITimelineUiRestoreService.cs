using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public interface ITimelineUiRestoreService
{
    void Restore(
        TimelineUiState preservedState,
        ObservableCollection<TrackViewModel> tracks,
        AudioTrackViewModel? audioTrack,
        JudgementLineEditorViewModel judgementLineEditor);
}
