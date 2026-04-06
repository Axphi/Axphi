using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineClipboardSelectionService : ITimelineClipboardSelectionService
{
    public List<TrackViewModel> GetSelectedJudgementLineTracks(ObservableCollection<TrackViewModel> tracks)
    {
        return tracks.Where(track => track.IsLayerSelected).ToList();
    }

    public int GetSelectedKeyframeCount(BpmTrackViewModel? bpmTrack, ObservableCollection<TrackViewModel> tracks)
    {
        int count = 0;

        if (bpmTrack != null)
        {
            count += bpmTrack.UIBpmKeyframes.Count(k => k.IsSelected);
        }

        foreach (var track in tracks)
        {
            count += track.UIAnchorKeyframes.Count(k => k.IsSelected);
            count += track.UIOffsetKeyframes.Count(k => k.IsSelected);
            count += track.UIScaleKeyframes.Count(k => k.IsSelected);
            count += track.UIRotationKeyframes.Count(k => k.IsSelected);
            count += track.UIOpacityKeyframes.Count(k => k.IsSelected);
            count += track.UISpeedKeyframes.Count(k => k.IsSelected);

            foreach (var note in track.UINotes)
            {
                if (note.IsSelected)
                {
                    count += 1;
                    continue;
                }

                count += note.UIAnchorKeyframes.Count(k => k.IsSelected);
                count += note.UIOffsetKeyframes.Count(k => k.IsSelected);
                count += note.UIScaleKeyframes.Count(k => k.IsSelected);
                count += note.UIRotationKeyframes.Count(k => k.IsSelected);
                count += note.UIOpacityKeyframes.Count(k => k.IsSelected);
                count += note.UINoteKindKeyframes.Count(k => k.IsSelected);
            }
        }

        return count;
    }
}
