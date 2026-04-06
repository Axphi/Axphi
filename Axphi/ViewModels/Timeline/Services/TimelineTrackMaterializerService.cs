using Axphi.Data;
using System.Collections.Generic;

namespace Axphi.ViewModels;

public sealed class TimelineTrackMaterializerService : ITimelineTrackMaterializerService
{
    public IReadOnlyList<TrackViewModel> BuildTracks(Chart chart, ITimelineTrackFactory trackFactory, TimelineViewModel timeline)
    {
        var result = new List<TrackViewModel>();

        if (chart.JudgementLines == null)
        {
            return result;
        }

        for (int i = 0; i < chart.JudgementLines.Count; i++)
        {
            var line = chart.JudgementLines[i];
            var track = trackFactory.CreateTrack(line, $"判定线图层 {i + 1}", timeline);
            result.Add(track);
        }

        return result;
    }
}
