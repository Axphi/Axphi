using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineSnapRuntime
{
    public TimelineSnapRuntime(
        bool isSnapModifierActive,
        double exactTickDouble,
        bool isPlayhead,
        double pixelsPerTick,
        int playheadTick,
        BpmTrackViewModel? bpmTrack,
        ObservableCollection<TrackViewModel> tracks)
    {
        IsSnapModifierActive = isSnapModifierActive;
        ExactTickDouble = exactTickDouble;
        IsPlayhead = isPlayhead;
        PixelsPerTick = pixelsPerTick;
        PlayheadTick = playheadTick;
        BpmTrack = bpmTrack;
        Tracks = tracks;
    }

    public bool IsSnapModifierActive { get; }

    public double ExactTickDouble { get; }

    public bool IsPlayhead { get; }

    public double PixelsPerTick { get; }

    public int PlayheadTick { get; }

    public BpmTrackViewModel? BpmTrack { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; }
}
