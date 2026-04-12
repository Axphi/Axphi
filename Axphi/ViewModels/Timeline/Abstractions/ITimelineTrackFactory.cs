using Axphi.Data;
using Axphi.Services;
using System.Collections.Generic;

namespace Axphi.ViewModels
{
    public interface ITimelineTrackFactory
    {
        BpmTrackViewModel CreateBpmTrack(Chart chart, TimelineViewModel timeline);

        AudioTrackViewModel CreateAudioTrack(Chart chart, TimelineViewModel timeline, IProjectSession projectSession);

        TrackViewModel CreateTrack(JudgementLine line, string trackName, TimelineViewModel timeline);

        IReadOnlyList<TrackViewModel> BuildTracks(Chart chart, TimelineViewModel timeline);
    }
}
