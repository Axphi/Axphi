using Axphi.Data;
using Axphi.Utilities;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axphi.ViewModels;

public sealed class TimelineStateService : ITimelineStateService
{
    private sealed record SnapshotDocument(Chart Chart, ProjectMetadata Metadata);

    private static readonly JsonSerializerOptions SnapshotJsonSerializerOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new VectorJsonConverter() }
    };

    public string SerializeSnapshot(Chart chart, ProjectMetadata metadata)
    {
        return JsonSerializer.Serialize(
            new SnapshotDocument(chart, CloneMetadata(metadata)),
            SnapshotJsonSerializerOptions);
    }

    public (Chart Chart, ProjectMetadata Metadata) DeserializeSnapshot(string snapshot)
    {
        var document = JsonSerializer.Deserialize<SnapshotDocument>(snapshot, SnapshotJsonSerializerOptions)
            ?? new SnapshotDocument(new Chart(), new ProjectMetadata());

        return (document.Chart, document.Metadata);
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
        var trackStates = runtime.Tracks
            .Select(track => new TrackUiState(track.Data.ID, track.IsExpanded, track.IsNoteExpanded))
            .ToList();

        JudgementLineEditorUiState? editorState = null;
        if (runtime.JudgementLineEditor.ActiveTrack != null)
        {
            editorState = new JudgementLineEditorUiState(
                runtime.JudgementLineEditor.ActiveTrack.Data.ID,
                runtime.JudgementLineEditor.CurrentNoteKind,
                runtime.JudgementLineEditor.HorizontalDivisions,
                runtime.JudgementLineEditor.ViewZoom,
                runtime.JudgementLineEditor.PanX,
                runtime.JudgementLineEditor.PanY);
        }

        return new TimelineUiState(
            runtime.ViewportActualWidth,
            trackStates,
            editorState);
    }

    public void RestoreUiState(
        TimelineUiState preservedState,
        ObservableCollection<TrackViewModel> tracks,
        JudgementLineEditorViewModel judgementLineEditor)
    {
        var trackStatesById = preservedState.Tracks.ToDictionary(track => track.TrackId);
        foreach (var track in tracks)
        {
            if (trackStatesById.TryGetValue(track.Data.ID, out var trackState))
            {
                track.IsExpanded = trackState.IsExpanded;
                track.IsNoteExpanded = trackState.IsNoteExpanded;
            }
        }

        if (preservedState.Editor is { } editorState)
        {
            var targetTrack = tracks.FirstOrDefault(track => track.Data.ID == editorState.ActiveTrackId);
            if (targetTrack != null)
            {
                judgementLineEditor.Open(targetTrack);
                judgementLineEditor.CurrentNoteKind = editorState.CurrentNoteKind;
                judgementLineEditor.HorizontalDivisions = editorState.HorizontalDivisions;
                judgementLineEditor.ViewZoom = editorState.ViewZoom;
                judgementLineEditor.PanX = editorState.PanX;
                judgementLineEditor.PanY = editorState.PanY;
            }
        }
    }

    public TimelinePlaybackRestoreState ResolvePlaybackState(ProjectMetadata metadata)
    {
        return new TimelinePlaybackRestoreState(
            metadata.CurrentHorizontalScrollOffset,
            metadata.WorkspaceStartTick,
            metadata.WorkspaceEndTick,
            metadata.PlayheadTimeSeconds,
            true);
    }

    private static ProjectMetadata CloneMetadata(ProjectMetadata metadata)
    {
        return new ProjectMetadata
        {
            AudioOffsetTicks = metadata.AudioOffsetTicks,
            AudioVolume = metadata.AudioVolume,
            PlayheadTimeSeconds = metadata.PlayheadTimeSeconds,
            CurrentHorizontalScrollOffset = metadata.CurrentHorizontalScrollOffset,
            ZoomScale = metadata.ZoomScale,
            TotalDurationTicks = metadata.TotalDurationTicks,
            WorkspaceStartTick = metadata.WorkspaceStartTick,
            WorkspaceEndTick = metadata.WorkspaceEndTick,
            IsAudioTrackExpanded = metadata.IsAudioTrackExpanded,
            IsAudioTrackLocked = metadata.IsAudioTrackLocked,
            PlaybackSpeed = metadata.PlaybackSpeed,
            BackgroundDimOpacity = metadata.BackgroundDimOpacity,
            PreserveAudioPitch = metadata.PreserveAudioPitch
        };
    }
}
