using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineUiStateService : ITimelineUiStateService
{
    public TimelineUiState Capture(TimelineCaptureRuntime runtime)
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
            runtime.CurrentPlayTimeSeconds,
            runtime.CurrentHorizontalScrollOffset,
            runtime.ZoomScale,
            runtime.ViewportActualWidth,
            runtime.WorkspaceStartTick,
            runtime.WorkspaceEndTick,
            runtime.IsAudioTrackExpanded,
            trackStates,
            editorState);
    }
}
