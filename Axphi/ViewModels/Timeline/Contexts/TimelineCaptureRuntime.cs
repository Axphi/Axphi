using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineCaptureRuntime
{
    public TimelineCaptureRuntime(
        double currentPlayTimeSeconds,
        double currentHorizontalScrollOffset,
        double zoomScale,
        double viewportActualWidth,
        int workspaceStartTick,
        int workspaceEndTick,
        bool isAudioTrackExpanded,
        ObservableCollection<TrackViewModel> tracks,
        JudgementLineEditorViewModel judgementLineEditor)
    {
        CurrentPlayTimeSeconds = currentPlayTimeSeconds;
        CurrentHorizontalScrollOffset = currentHorizontalScrollOffset;
        ZoomScale = zoomScale;
        ViewportActualWidth = viewportActualWidth;
        WorkspaceStartTick = workspaceStartTick;
        WorkspaceEndTick = workspaceEndTick;
        IsAudioTrackExpanded = isAudioTrackExpanded;
        Tracks = tracks;
        JudgementLineEditor = judgementLineEditor;
    }

    public double CurrentPlayTimeSeconds { get; }

    public double CurrentHorizontalScrollOffset { get; }

    public double ZoomScale { get; }

    public double ViewportActualWidth { get; }

    public int WorkspaceStartTick { get; }

    public int WorkspaceEndTick { get; }

    public bool IsAudioTrackExpanded { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    public JudgementLineEditorViewModel JudgementLineEditor { get; }
}
