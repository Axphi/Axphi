using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineMutationRuntime
{
    public TimelineMutationRuntime(
        IMessenger messenger,
        AudioTrackViewModel? audioTrack,
        BpmTrackViewModel? bpmTrack,
        ObservableCollection<TrackViewModel> tracks,
        int currentTick,
        KeyFrameEasingDirection easingDirection,
        bool broadcastSortMessage,
        bool syncNotes)
    {
        Messenger = messenger;
        AudioTrack = audioTrack;
        BpmTrack = bpmTrack;
        Tracks = tracks;
        CurrentTick = currentTick;
        EasingDirection = easingDirection;
        BroadcastSortMessage = broadcastSortMessage;
        SyncNotes = syncNotes;
    }

    public IMessenger Messenger { get; }

    public AudioTrackViewModel? AudioTrack { get; }

    public BpmTrackViewModel? BpmTrack { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    public int CurrentTick { get; }

    public KeyFrameEasingDirection EasingDirection { get; }

    public bool BroadcastSortMessage { get; }

    public bool SyncNotes { get; }
}
