using Axphi.Data;
using Axphi.Data.KeyFrames;
using System.Collections.Generic;

namespace Axphi.ViewModels;

public sealed class TimelinePlaybackSyncService : ITimelinePlaybackSyncService
{
    public void SyncTrackValuesToTime(
        int currentTick,
        KeyFrameEasingDirection easingDirection,
        BpmTrackViewModel? bpmTrack,
        IEnumerable<TrackViewModel> tracks)
    {
        bpmTrack?.SyncValuesToTime(currentTick);

        foreach (var track in tracks)
        {
            track.SyncValuesToTime(currentTick, easingDirection);
            foreach (var note in track.UINotes)
            {
                note.SyncValuesToTime(currentTick, easingDirection);
            }
        }
    }
}
