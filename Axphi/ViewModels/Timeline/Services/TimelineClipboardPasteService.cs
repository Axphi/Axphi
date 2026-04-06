using Axphi.Data;
using Axphi.Data.KeyFrames;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Axphi.ViewModels;

public sealed class TimelineClipboardPasteService : ITimelineClipboardPasteService
{
    public object? PasteClipboardItem(TimelinePasteRuntime runtime, KeyframeClipboardItem item, int targetTime)
    {
        return item.Target switch
        {
            KeyframeClipboardTarget.Bpm => PasteBpmKeyframe(runtime, targetTime, (double)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.TrackAnchor when item.Owner is TrackViewModel track => PasteTrackAnchorKeyframe(runtime, track, targetTime, (Vector)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.TrackOffset when item.Owner is TrackViewModel track => PasteTrackOffsetKeyframe(runtime, track, targetTime, (Vector)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.TrackScale when item.Owner is TrackViewModel track => PasteTrackScaleKeyframe(runtime, track, targetTime, (Vector)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.TrackRotation when item.Owner is TrackViewModel track => PasteTrackRotationKeyframe(runtime, track, targetTime, (double)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.TrackOpacity when item.Owner is TrackViewModel track => PasteTrackOpacityKeyframe(runtime, track, targetTime, (double)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.TrackSpeed when item.Owner is TrackViewModel track => PasteTrackSpeedKeyframe(runtime, track, targetTime, (double)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.NoteBody when item.Owner is TrackViewModel track => PasteNoteBody(runtime, track, targetTime, (Note)item.Value),
            KeyframeClipboardTarget.NoteAnchor when item.Owner is NoteViewModel note => PasteNoteAnchorKeyframe(runtime, note, targetTime, (Vector)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.NoteOffset when item.Owner is NoteViewModel note => PasteNoteOffsetKeyframe(runtime, note, targetTime, (Vector)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.NoteScale when item.Owner is NoteViewModel note => PasteNoteScaleKeyframe(runtime, note, targetTime, (Vector)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.NoteRotation when item.Owner is NoteViewModel note => PasteNoteRotationKeyframe(runtime, note, targetTime, (double)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.NoteOpacity when item.Owner is NoteViewModel note => PasteNoteOpacityKeyframe(runtime, note, targetTime, (double)item.Value, item.Easing, item.IsFreezeKeyframe),
            KeyframeClipboardTarget.NoteKind when item.Owner is NoteViewModel note => PasteNoteKindKeyframe(runtime, note, targetTime, (NoteKind)item.Value, item.Easing, item.IsFreezeKeyframe),
            _ => null,
        };
    }

    private static KeyFrameUIWrapper<double>? PasteBpmKeyframe(TimelinePasteRuntime runtime, int targetTime, double value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (runtime.BpmTrack == null)
        {
            return null;
        }

        return UpsertKeyframe(runtime.Timeline, runtime.CurrentChart.BpmKeyFrames, runtime.BpmTrack.UIBpmKeyframes, new KeyFrame<double>
        {
            Time = targetTime,
            Value = value,
            Easing = easing,
            IsFreezeKeyframe = isFreezeKeyframe,
        });
    }

    private static KeyFrameUIWrapper<Vector>? PasteTrackAnchorKeyframe(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, Vector value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!runtime.Tracks.Contains(track)) return null;
        return UpsertKeyframe(runtime.Timeline, track.Data.AnimatableProperties.Anchor.KeyFrames, track.UIAnchorKeyframes, new OffsetKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<Vector>? PasteTrackOffsetKeyframe(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, Vector value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!runtime.Tracks.Contains(track)) return null;
        return UpsertKeyframe(runtime.Timeline, track.Data.AnimatableProperties.Offset.KeyFrames, track.UIOffsetKeyframes, new OffsetKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<Vector>? PasteTrackScaleKeyframe(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, Vector value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!runtime.Tracks.Contains(track)) return null;
        return UpsertKeyframe(runtime.Timeline, track.Data.AnimatableProperties.Scale.KeyFrames, track.UIScaleKeyframes, new ScaleKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<double>? PasteTrackRotationKeyframe(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, double value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!runtime.Tracks.Contains(track)) return null;
        return UpsertKeyframe(runtime.Timeline, track.Data.AnimatableProperties.Rotation.KeyFrames, track.UIRotationKeyframes, new RotationKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<double>? PasteTrackOpacityKeyframe(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, double value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!runtime.Tracks.Contains(track)) return null;
        return UpsertKeyframe(runtime.Timeline, track.Data.AnimatableProperties.Opacity.KeyFrames, track.UIOpacityKeyframes, new OpacityKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<double>? PasteTrackSpeedKeyframe(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, double value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!runtime.Tracks.Contains(track)) return null;
        return UpsertKeyframe(runtime.Timeline, track.Data.SpeedKeyFrames, track.UISpeedKeyframes, new KeyFrame<double> { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static NoteViewModel? PasteNoteBody(TimelinePasteRuntime runtime, TrackViewModel track, int targetTime, Note sourceNote)
    {
        if (!runtime.Tracks.Contains(track))
        {
            return null;
        }

        var clonedNote = runtime.ClipboardCloneService.CloneNote(sourceNote);
        clonedNote.HitTime = Math.Max(0, targetTime);

        track.Data.Notes ??= new List<Note>();
        track.Data.Notes.Add(clonedNote);

        var newNoteViewModel = new NoteViewModel(clonedNote, runtime.Timeline, track);
        newNoteViewModel.SyncValuesToTime(runtime.Timeline.GetCurrentTick(), runtime.CurrentChart.KeyFrameEasingDirection);
        track.UINotes.Add(newNoteViewModel);
        return newNoteViewModel;
    }

    private static KeyFrameUIWrapper<Vector>? PasteNoteAnchorKeyframe(TimelinePasteRuntime runtime, NoteViewModel note, int targetTime, Vector value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!note.ParentTrack.UINotes.Contains(note)) return null;
        return UpsertKeyframe(runtime.Timeline, note.Model.AnimatableProperties.Anchor.KeyFrames, note.UIAnchorKeyframes, new OffsetKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<Vector>? PasteNoteOffsetKeyframe(TimelinePasteRuntime runtime, NoteViewModel note, int targetTime, Vector value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!note.ParentTrack.UINotes.Contains(note)) return null;
        return UpsertKeyframe(runtime.Timeline, note.Model.AnimatableProperties.Offset.KeyFrames, note.UIOffsetKeyframes, new OffsetKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<Vector>? PasteNoteScaleKeyframe(TimelinePasteRuntime runtime, NoteViewModel note, int targetTime, Vector value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!note.ParentTrack.UINotes.Contains(note)) return null;
        return UpsertKeyframe(runtime.Timeline, note.Model.AnimatableProperties.Scale.KeyFrames, note.UIScaleKeyframes, new ScaleKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<double>? PasteNoteRotationKeyframe(TimelinePasteRuntime runtime, NoteViewModel note, int targetTime, double value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!note.ParentTrack.UINotes.Contains(note)) return null;
        return UpsertKeyframe(runtime.Timeline, note.Model.AnimatableProperties.Rotation.KeyFrames, note.UIRotationKeyframes, new RotationKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<double>? PasteNoteOpacityKeyframe(TimelinePasteRuntime runtime, NoteViewModel note, int targetTime, double value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!note.ParentTrack.UINotes.Contains(note)) return null;
        return UpsertKeyframe(runtime.Timeline, note.Model.AnimatableProperties.Opacity.KeyFrames, note.UIOpacityKeyframes, new OpacityKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<NoteKind>? PasteNoteKindKeyframe(TimelinePasteRuntime runtime, NoteViewModel note, int targetTime, NoteKind value, BezierEasing easing, bool isFreezeKeyframe)
    {
        if (!note.ParentTrack.UINotes.Contains(note)) return null;
        return UpsertKeyframe(runtime.Timeline, note.Model.KindKeyFrames, note.UINoteKindKeyframes, new NoteKindKeyFrame { Time = targetTime, Value = value, Easing = easing, IsFreezeKeyframe = isFreezeKeyframe });
    }

    private static KeyFrameUIWrapper<T> UpsertKeyframe<T, TKeyFrame>(TimelineViewModel timeline, List<TKeyFrame> dataList, ObservableCollection<KeyFrameUIWrapper<T>> uiList, TKeyFrame frame)
        where T : struct
        where TKeyFrame : KeyFrame<T>
    {
        var existingWrapper = uiList.FirstOrDefault(wrapper => wrapper.Model.Time == frame.Time);
        if (existingWrapper != null)
        {
            existingWrapper.Model.Value = frame.Value;
            existingWrapper.Model.Easing = frame.Easing;
            existingWrapper.IsFreezeKeyframe = frame.IsFreezeKeyframe;
            return existingWrapper;
        }

        dataList.Add(frame);
        dataList.Sort((a, b) => a.Time.CompareTo(b.Time));

        var newWrapper = new KeyFrameUIWrapper<T>(frame, timeline);
        uiList.Add(newWrapper);
        return newWrapper;
    }
}
