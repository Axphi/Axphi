using Axphi.Data;
using System.Collections.Generic;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineClipboardCollectorService : ITimelineClipboardCollectorService
{
    private readonly ITimelineClipboardCloneService _cloneService;

    public TimelineClipboardCollectorService(ITimelineClipboardCloneService cloneService)
    {
        _cloneService = cloneService;
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
        foreach (var wrapper in wrappers.Where(w => w.IsSelected))
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
                _cloneService.CloneNote(note.Model),
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
}
