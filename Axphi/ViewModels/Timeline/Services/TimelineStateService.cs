using Axphi.Data;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineStateService : ITimelineStateService
{
    private readonly ITimelineSnapshotService _snapshotService;
    private readonly ITimelineUiStateService _uiStateService;
    private readonly ITimelineUiRestoreService _uiRestoreService;
    private readonly ITimelinePlaybackRestoreService _playbackRestoreService;

    public TimelineStateService(
        ITimelineSnapshotService snapshotService,
        ITimelineUiStateService uiStateService,
        ITimelineUiRestoreService uiRestoreService,
        ITimelinePlaybackRestoreService playbackRestoreService)
    {
        _snapshotService = snapshotService;
        _uiStateService = uiStateService;
        _uiRestoreService = uiRestoreService;
        _playbackRestoreService = playbackRestoreService;
    }

    public string SerializeSnapshot(Chart chart, ProjectMetadata metadata)
    {
        return _snapshotService.Serialize(chart, metadata);
    }

    public (Chart Chart, ProjectMetadata Metadata) DeserializeSnapshot(string snapshot)
    {
        return _snapshotService.Deserialize(snapshot);
    }

    public Project RestoreProjectFromSnapshot(string snapshot, Project currentProject)
    {
        var restored = DeserializeSnapshot(snapshot);
        return new Project
        {
            Chart = restored.Chart,
            Metadata = restored.Metadata,
            EncodedAudio = currentProject.EncodedAudio,
            EncodedIllustration = currentProject.EncodedIllustration
        };
    }

    public TimelineUiState CaptureUiState(TimelineCaptureRuntime runtime)
    {
        return _uiStateService.Capture(runtime);
    }

    public void RestoreUiState(
        TimelineUiState preservedState,
        ObservableCollection<TrackViewModel> tracks,
        AudioTrackViewModel? audioTrack,
        JudgementLineEditorViewModel judgementLineEditor)
    {
        _uiRestoreService.Restore(preservedState, tracks, audioTrack, judgementLineEditor);
    }

    public TimelinePlaybackRestoreState ResolvePlaybackState(TimelineUiState? preservedUiState, ProjectMetadata metadata)
    {
        return _playbackRestoreService.Resolve(preservedUiState, metadata);
    }
}
