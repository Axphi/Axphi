using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace Axphi.ViewModels;

public sealed class TimelineClipboardService : ITimelineClipboardService
{
    private static readonly JsonSerializerOptions CloneJsonSerializerOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new VectorJsonConverter() }
    };

    public List<TrackViewModel> GetSelectedJudgementLineTracks(ObservableCollection<TrackViewModel> tracks)
    {
        return tracks.Where(track => track.IsLayerSelected).ToList();
    }

    public int GetSelectedKeyframeCount(BpmTrackViewModel? bpmTrack, ObservableCollection<TrackViewModel> tracks)
    {
        int count = 0;

        if (bpmTrack != null)
        {
            count += bpmTrack.UIBpmKeyframes.Count(keyframe => keyframe.IsSelected);
        }

        foreach (var track in tracks)
        {
            count += track.UIAnchorKeyframes.Count(keyframe => keyframe.IsSelected);
            count += track.UIOffsetKeyframes.Count(keyframe => keyframe.IsSelected);
            count += track.UIScaleKeyframes.Count(keyframe => keyframe.IsSelected);
            count += track.UIRotationKeyframes.Count(keyframe => keyframe.IsSelected);
            count += track.UIOpacityKeyframes.Count(keyframe => keyframe.IsSelected);
            count += track.UISpeedKeyframes.Count(keyframe => keyframe.IsSelected);

            foreach (var note in track.UINotes)
            {
                if (note.IsSelected)
                {
                    count += 1;
                    continue;
                }

                count += note.UIAnchorKeyframes.Count(keyframe => keyframe.IsSelected);
                count += note.UIOffsetKeyframes.Count(keyframe => keyframe.IsSelected);
                count += note.UIScaleKeyframes.Count(keyframe => keyframe.IsSelected);
                count += note.UIRotationKeyframes.Count(keyframe => keyframe.IsSelected);
                count += note.UIOpacityKeyframes.Count(keyframe => keyframe.IsSelected);
                count += note.UINoteKindKeyframes.Count(keyframe => keyframe.IsSelected);
            }
        }

        return count;
    }

    public Note CloneNote(Note note)
    {
        var clonedNote = JsonSerializer.Deserialize<Note>(
                JsonSerializer.Serialize(note, CloneJsonSerializerOptions),
                CloneJsonSerializerOptions)
            ?? new Note();

        clonedNote.ID = Guid.NewGuid().ToString();
        return clonedNote;
    }

    public JudgementLine CloneJudgementLine(JudgementLine line)
    {
        var clonedLine = JsonSerializer.Deserialize<JudgementLine>(
                JsonSerializer.Serialize(line, CloneJsonSerializerOptions),
                CloneJsonSerializerOptions)
            ?? new JudgementLine();

        clonedLine.ID = Guid.NewGuid().ToString();
        clonedLine.Notes ??= new List<Note>();
        foreach (var note in clonedLine.Notes)
        {
            note.ID = Guid.NewGuid().ToString();
        }

        return clonedLine;
    }

    public List<JudgementLine> CloneJudgementLinesWithMappedParents(IEnumerable<JudgementLine> lines)
    {
        var sourceLines = lines.ToList();
        var clonedLines = new List<JudgementLine>(sourceLines.Count);
        var idMap = new Dictionary<string, string>(sourceLines.Count);

        foreach (var line in sourceLines)
        {
            var clonedLine = CloneJudgementLine(line);
            clonedLines.Add(clonedLine);
            idMap[line.ID] = clonedLine.ID;
        }

        foreach (var clonedLine in clonedLines)
        {
            if (!string.IsNullOrWhiteSpace(clonedLine.ParentLineId)
                && idMap.TryGetValue(clonedLine.ParentLineId, out var mappedParentId))
            {
                clonedLine.ParentLineId = mappedParentId;
            }
        }

        return clonedLines;
    }

    public void AddClipboardItem(
        ICollection<KeyframeClipboardItem> clipboard,
        HashSet<string> copiedKeys,
        KeyframeClipboardTarget target,
        object? owner,
        int time,
        object value,
        BezierEasing easing,
        bool isFreezeKeyframe = false,
        string? uniqueKey = null)
    {
        string key = uniqueKey ?? $"{target}|{owner?.GetHashCode() ?? 0}|{time}";
        if (copiedKeys.Add(key))
        {
            clipboard.Add(new KeyframeClipboardItem(target, owner, time, value, easing, isFreezeKeyframe));
        }
    }

    public void AddSelectedWrappersToClipboard<T>(
        ICollection<KeyframeClipboardItem> clipboard,
        IEnumerable<KeyFrameUIWrapper<T>> wrappers,
        KeyframeClipboardTarget target,
        object? owner,
        HashSet<string> copiedKeys)
        where T : struct
    {
        foreach (var wrapper in wrappers.Where(wrapper => wrapper.IsSelected))
        {
            AddClipboardItem(clipboard, copiedKeys, target, owner, wrapper.Model.Time, wrapper.Model.Value, wrapper.Model.Easing, wrapper.IsFreezeKeyframe);
        }
    }

    public void AddNoteSelectionToClipboard(
        ICollection<KeyframeClipboardItem> clipboard,
        NoteViewModel note,
        TrackViewModel track,
        HashSet<string> copiedKeys)
    {
        if (note.IsSelected)
        {
            AddClipboardItem(
                clipboard,
                copiedKeys,
                KeyframeClipboardTarget.NoteBody,
                track,
                note.HitTime,
                CloneNote(note.Model),
                default,
                uniqueKey: $"{KeyframeClipboardTarget.NoteBody}|{track.GetHashCode()}|{note.Model.ID}");
            return;
        }

        AddSelectedWrappersToClipboard(clipboard, note.UIOffsetKeyframes, KeyframeClipboardTarget.NoteOffset, note, copiedKeys);
        AddSelectedWrappersToClipboard(clipboard, note.UIAnchorKeyframes, KeyframeClipboardTarget.NoteAnchor, note, copiedKeys);
        AddSelectedWrappersToClipboard(clipboard, note.UIScaleKeyframes, KeyframeClipboardTarget.NoteScale, note, copiedKeys);
        AddSelectedWrappersToClipboard(clipboard, note.UIRotationKeyframes, KeyframeClipboardTarget.NoteRotation, note, copiedKeys);
        AddSelectedWrappersToClipboard(clipboard, note.UIOpacityKeyframes, KeyframeClipboardTarget.NoteOpacity, note, copiedKeys);
        AddSelectedWrappersToClipboard(clipboard, note.UINoteKindKeyframes, KeyframeClipboardTarget.NoteKind, note, copiedKeys);
    }

    public void ApplySelectionToPastedItems(IEnumerable<object> pastedItems)
    {
        foreach (var item in pastedItems)
        {
            switch (item)
            {
                case KeyFrameUIWrapper<double> doubleWrapper:
                    doubleWrapper.IsSelected = true;
                    break;
                case KeyFrameUIWrapper<Vector> vectorWrapper:
                    vectorWrapper.IsSelected = true;
                    break;
                case KeyFrameUIWrapper<NoteKind> kindWrapper:
                    kindWrapper.IsSelected = true;
                    break;
                case NoteViewModel noteViewModel:
                    noteViewModel.ParentTrack.IsNoteExpanded = true;
                    noteViewModel.IsSelected = true;
                    break;
            }
        }
    }

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

        var clonedNote = runtime.ClipboardService.CloneNote(sourceNote);
        clonedNote.HitTime = Math.Max(0, targetTime);

        track.Data.Notes ??= new List<Note>();
        track.Data.Notes.Add(clonedNote);

        var newNoteViewModel = new NoteViewModel(clonedNote, runtime.Timeline, track, runtime.Messenger);
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
