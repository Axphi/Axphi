using Axphi.Data;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelinePasteRuntime
{
    public TimelinePasteRuntime(
        Chart currentChart,
        BpmTrackViewModel? bpmTrack,
        ObservableCollection<TrackViewModel> tracks,
        TimelineViewModel timeline,
        ITimelineClipboardService clipboardService)
    {
        CurrentChart = currentChart;
        BpmTrack = bpmTrack;
        Tracks = tracks;
        Timeline = timeline;
        ClipboardService = clipboardService;
    }

    public Chart CurrentChart { get; }

    public BpmTrackViewModel? BpmTrack { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    public TimelineViewModel Timeline { get; }

    public ITimelineClipboardService ClipboardService { get; }
}
