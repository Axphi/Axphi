using Axphi.Data;
using Axphi.Services;

namespace Axphi.ViewModels
{
    public interface ITimelineTrackFactory
    {
        BpmTrackViewModel CreateBpmTrack(Chart chart, TimelineViewModel timeline);

        AudioTrackViewModel CreateAudioTrack(Chart chart, TimelineViewModel timeline, ProjectManager projectManager);

        TrackViewModel CreateTrack(JudgementLine line, string trackName, TimelineViewModel timeline);
    }
}
