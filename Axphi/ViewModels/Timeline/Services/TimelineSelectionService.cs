using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineSelectionService : ITimelineSelectionService
{
    public bool IsTrackLevelKeyframeWrapperSelected(IEnumerable<TrackViewModel> tracks, object wrapper)
    {
        return tracks
            .SelectMany(EnumerateTrackLevelKeyframes)
            .Any(keyframe => ReferenceEquals(keyframe, wrapper));
    }

    public int GetSelectedTrackLevelKeyframeCount(IEnumerable<TrackViewModel> tracks)
    {
        return tracks
            .SelectMany(EnumerateTrackLevelKeyframes)
            .Count(keyframe => keyframe.IsSelected);
    }

    public void SetFreezeStateForSelectedTrackLevelKeyframes(IEnumerable<TrackViewModel> tracks, bool isFreeze)
    {
        foreach (var keyframe in tracks
            .SelectMany(EnumerateTrackLevelKeyframes)
            .Where(keyframe => keyframe.IsSelected))
        {
            keyframe.IsFreezeKeyframe = isFreeze;
        }
    }

    public bool ApplyEasingToSelectedKeyframes(BpmTrackViewModel? bpmTrack, IEnumerable<TrackViewModel> tracks, BezierEasing easing)
    {
        bool hasModified = false;
        foreach (var keyframe in EnumerateAllEditableKeyframes(bpmTrack, tracks).Where(item => item.IsSelected))
        {
            keyframe.ApplyEasing(easing);
            hasModified = true;
        }

        return hasModified;
    }

    public bool HasSelectedEditableKeyframes(BpmTrackViewModel? bpmTrack, IEnumerable<TrackViewModel> tracks)
    {
        return EnumerateAllEditableKeyframes(bpmTrack, tracks).Any(keyframe => keyframe.IsSelected);
    }

    public TrackViewModel? RefreshSelection(
        IEnumerable<TrackViewModel> tracks,
        TrackViewModel? activeOwner,
        NoteSelectionPanelViewModel panel,
        TrackViewModel? preferredOwner = null,
        NoteViewModel? preferredSingle = null)
    {
        var trackList = tracks.ToList();
        var selectedEntries = trackList
            .SelectMany(track => track.UINotes.Where(note => note.IsSelected).Select(note => (track, note)))
            .ToList();

        foreach (var track in trackList)
        {
            track.SelectedNote = null;
            track.IsNotePanelOwner = false;
        }

        if (selectedEntries.Count == 0)
        {
            var owner = preferredOwner ?? activeOwner;
            if (owner != null)
            {
                owner.IsNotePanelOwner = true;
            }

            panel.SyncSelection(Array.Empty<NoteViewModel>());
            return owner;
        }

        TrackViewModel ownerTrack;
        if (preferredOwner != null && selectedEntries.Any(entry => ReferenceEquals(entry.track, preferredOwner)))
        {
            ownerTrack = preferredOwner;
        }
        else if (activeOwner != null && selectedEntries.Any(entry => ReferenceEquals(entry.track, activeOwner)))
        {
            ownerTrack = activeOwner;
        }
        else
        {
            ownerTrack = selectedEntries[0].track;
        }

        ownerTrack.IsNotePanelOwner = true;

        if (selectedEntries.Count == 1)
        {
            var selectedNote = preferredSingle != null && selectedEntries.Any(entry => ReferenceEquals(entry.note, preferredSingle))
                ? preferredSingle
                : selectedEntries[0].note;
            ownerTrack = selectedEntries.First(entry => ReferenceEquals(entry.note, selectedNote)).track;
            ownerTrack.IsNotePanelOwner = true;
            ownerTrack.SelectedNote = selectedNote;
        }

        panel.SyncSelection(selectedEntries.Select(entry => entry.note).ToList());
        return ownerTrack;
    }

    private static IEnumerable<IKeyFrameUiItem> EnumerateTrackLevelKeyframes(TrackViewModel track)
    {
        foreach (var keyframe in track.UIAnchorKeyframes) yield return keyframe;
        foreach (var keyframe in track.UIOffsetKeyframes) yield return keyframe;
        foreach (var keyframe in track.UIScaleKeyframes) yield return keyframe;
        foreach (var keyframe in track.UIRotationKeyframes) yield return keyframe;
        foreach (var keyframe in track.UIOpacityKeyframes) yield return keyframe;
        foreach (var keyframe in track.UISpeedKeyframes) yield return keyframe;
    }

    private static IEnumerable<IKeyFrameUiItem> EnumerateNoteLevelKeyframes(NoteViewModel note)
    {
        foreach (var keyframe in note.UIAnchorKeyframes) yield return keyframe;
        foreach (var keyframe in note.UIOffsetKeyframes) yield return keyframe;
        foreach (var keyframe in note.UIScaleKeyframes) yield return keyframe;
        foreach (var keyframe in note.UIRotationKeyframes) yield return keyframe;
        foreach (var keyframe in note.UIOpacityKeyframes) yield return keyframe;
        foreach (var keyframe in note.UINoteKindKeyframes) yield return keyframe;
    }

    private static IEnumerable<IKeyFrameUiItem> EnumerateAllEditableKeyframes(BpmTrackViewModel? bpmTrack, IEnumerable<TrackViewModel> tracks)
    {
        if (bpmTrack != null)
        {
            foreach (var keyframe in bpmTrack.UIBpmKeyframes)
            {
                yield return keyframe;
            }
        }

        foreach (var track in tracks)
        {
            foreach (var keyframe in EnumerateTrackLevelKeyframes(track))
            {
                yield return keyframe;
            }

            foreach (var note in track.UINotes)
            {
                foreach (var keyframe in EnumerateNoteLevelKeyframes(note))
                {
                    yield return keyframe;
                }
            }
        }
    }
}
