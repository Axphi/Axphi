using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public interface ITimelineClipboardSelectionService
{
    List<TrackViewModel> GetSelectedJudgementLineTracks(ObservableCollection<TrackViewModel> tracks);

    int GetSelectedKeyframeCount(BpmTrackViewModel? bpmTrack, ObservableCollection<TrackViewModel> tracks);
}
