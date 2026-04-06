using Axphi.Data;
using Axphi.Data.KeyFrames;
using System.Collections.Generic;

namespace Axphi.ViewModels;

public interface ITimelinePlaybackSyncService
{
    void SyncTrackValuesToTime(
        int currentTick,
        KeyFrameEasingDirection easingDirection,
        BpmTrackViewModel? bpmTrack,
        IEnumerable<TrackViewModel> tracks);
}
