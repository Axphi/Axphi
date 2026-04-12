using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;

namespace Axphi.ViewModels
{
    public sealed class TimelineTrackFactory : ITimelineTrackFactory
    {
        private readonly IMessenger _messenger;

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

        public IReadOnlyList<TrackViewModel> BuildTracks(Chart chart, TimelineViewModel timeline)
        {
            var result = new List<TrackViewModel>();

            if (chart.JudgementLines == null)
            {
                return result;
            }

            for (int i = 0; i < chart.JudgementLines.Count; i++)
            {
                var line = chart.JudgementLines[i];
                result.Add(CreateTrack(line, $"判定线图层 {i + 1}", timeline));
            }

            return result;
        }
    }
}
