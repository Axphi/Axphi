using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineCaptureRuntime
{
    public TimelineCaptureRuntime(
        double viewportActualWidth,
        ObservableCollection<TrackViewModel> tracks,
        JudgementLineEditorViewModel judgementLineEditor)
    {
        ViewportActualWidth = viewportActualWidth;
        Tracks = tracks;
        JudgementLineEditor = judgementLineEditor;
    }

    public double ViewportActualWidth { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    public JudgementLineEditorViewModel JudgementLineEditor { get; }
}
