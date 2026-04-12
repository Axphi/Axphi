using Axphi.Data;
using System;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineDeleteRuntime
{
    public TimelineDeleteRuntime(
        Chart currentChart,
        ObservableCollection<TrackViewModel> tracks,
        BpmTrackViewModel? bpmTrack,
        AudioTrackViewModel? audioTrack,
        Action<TimelineSelectionContext> setSelectionContext,
        Action reindexTrackNames,
        Action refreshParentLineBindings)
    {
        CurrentChart = currentChart;
        Tracks = tracks;
        BpmTrack = bpmTrack;
        AudioTrack = audioTrack;
        SetSelectionContext = setSelectionContext;
        ReindexTrackNames = reindexTrackNames;
        RefreshParentLineBindings = refreshParentLineBindings;
    }

    public Chart CurrentChart { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    public BpmTrackViewModel? BpmTrack { get; }

    public AudioTrackViewModel? AudioTrack { get; }

    public Action<TimelineSelectionContext> SetSelectionContext { get; }

    public Action ReindexTrackNames { get; }

    public Action RefreshParentLineBindings { get; }
}
