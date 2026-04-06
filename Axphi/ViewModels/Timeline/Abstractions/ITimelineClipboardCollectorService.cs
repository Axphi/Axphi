using Axphi.Data;

namespace Axphi.ViewModels;

public interface ITimelineClipboardCollectorService
{
    void AddClipboardItem(
        ICollection<KeyframeClipboardItem> clipboard,
        HashSet<string> copiedKeys,
        KeyframeClipboardTarget target,
        object? owner,
        int time,
        object value,
        BezierEasing easing,
        bool isFreezeKeyframe = false,
        string? uniqueKey = null);

    void AddSelectedWrappersToClipboard<T>(
        ICollection<KeyframeClipboardItem> clipboard,
        IEnumerable<KeyFrameUIWrapper<T>> wrappers,
        KeyframeClipboardTarget target,
        object? owner,
        HashSet<string> copiedKeys)
        where T : struct;

    void AddNoteSelectionToClipboard(
        ICollection<KeyframeClipboardItem> clipboard,
        NoteViewModel note,
        TrackViewModel track,
        HashSet<string> copiedKeys);
}
