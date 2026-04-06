using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace Axphi.ViewModels
{
    public sealed class TimelineTrackFactory : ITimelineTrackFactory
    {
        private readonly IMessenger _messenger;

        public TimelineTrackFactory()
            : this(WeakReferenceMessenger.Default)
        {
        }

        public TimelineTrackFactory(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public BpmTrackViewModel CreateBpmTrack(Chart chart, TimelineViewModel timeline)
        {
            return new BpmTrackViewModel(chart, timeline, _messenger);
        }

        public AudioTrackViewModel CreateAudioTrack(Chart chart, TimelineViewModel timeline, IProjectSession projectSession)
        {
            return new AudioTrackViewModel(chart, timeline, projectSession, _messenger);
        }

        public TrackViewModel CreateTrack(JudgementLine line, string trackName, TimelineViewModel timeline)
        {
            return new TrackViewModel(line, trackName, timeline, _messenger);
        }
    }
}
