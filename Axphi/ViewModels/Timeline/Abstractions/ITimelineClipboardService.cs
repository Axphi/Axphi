using Axphi.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public interface ITimelineClipboardService
{
    List<TrackViewModel> GetSelectedJudgementLineTracks(ObservableCollection<TrackViewModel> tracks);

    int GetSelectedKeyframeCount(BpmTrackViewModel? bpmTrack, ObservableCollection<TrackViewModel> tracks);

    Note CloneNote(Note note);

    JudgementLine CloneJudgementLine(JudgementLine line);

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

    object? PasteClipboardItem(TimelinePasteRuntime runtime, KeyframeClipboardItem item, int targetTime);
}
