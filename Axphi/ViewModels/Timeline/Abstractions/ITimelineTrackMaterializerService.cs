using Axphi.Data;
using System.Collections.Generic;

namespace Axphi.ViewModels;

public interface ITimelineTrackMaterializerService
{
    IReadOnlyList<TrackViewModel> BuildTracks(Chart chart, ITimelineTrackFactory trackFactory, TimelineViewModel timeline);
}
