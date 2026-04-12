using CommunityToolkit.Mvvm.Messaging;

namespace Axphi.ViewModels;

public sealed class TimelineMutationSyncService : ITimelineMutationSyncService
{
    public void SyncAfterMutation(TimelineMutationRuntime runtime)
    {
        if (runtime.BroadcastSortMessage)
        {
            runtime.Messenger.Send(new KeyframesNeedSortMessage());
        }

        runtime.Messenger.Send(new JudgementLinesChangedMessage());
        runtime.AudioTrack?.UpdatePixels();

        runtime.BpmTrack?.SyncValuesToTime(runtime.CurrentTick);

        foreach (var track in runtime.Tracks)
        {
            track.SyncValuesToTime(runtime.CurrentTick, runtime.EasingDirection);

            if (!runtime.SyncNotes)
            {
                continue;
            }

            foreach (var note in track.UINotes)
            {
                note.SyncValuesToTime(runtime.CurrentTick, runtime.EasingDirection);
            }
        }
    }
}
