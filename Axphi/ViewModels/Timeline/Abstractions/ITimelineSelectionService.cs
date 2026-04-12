using Axphi.Data;
using System.Collections.Generic;

namespace Axphi.ViewModels;

public interface ITimelineSelectionService
{
    bool IsTrackLevelKeyframeWrapperSelected(IEnumerable<TrackViewModel> tracks, object wrapper);

    int GetSelectedTrackLevelKeyframeCount(IEnumerable<TrackViewModel> tracks);

    void SetFreezeStateForSelectedTrackLevelKeyframes(IEnumerable<TrackViewModel> tracks, bool isFreeze);

    bool ApplyEasingToSelectedKeyframes(BpmTrackViewModel? bpmTrack, IEnumerable<TrackViewModel> tracks, BezierEasing easing);

    bool HasSelectedEditableKeyframes(BpmTrackViewModel? bpmTrack, IEnumerable<TrackViewModel> tracks);

    TrackViewModel? RefreshSelection(
        IEnumerable<TrackViewModel> tracks,
        TrackViewModel? activeOwner,
        NoteSelectionPanelViewModel panel,
        TrackViewModel? preferredOwner = null,
        NoteViewModel? preferredSingle = null);
}
