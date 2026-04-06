using System.Linq;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineUiRestoreService : ITimelineUiRestoreService
{
    public void Restore(
        TimelineUiState preservedState,
        ObservableCollection<TrackViewModel> tracks,
        AudioTrackViewModel? audioTrack,
        JudgementLineEditorViewModel judgementLineEditor)
    {
        if (audioTrack != null)
        {
            audioTrack.IsExpanded = preservedState.IsAudioTrackExpanded;
        }

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
}
