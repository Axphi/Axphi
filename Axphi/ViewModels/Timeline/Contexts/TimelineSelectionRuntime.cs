using System;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public sealed class TimelineSelectionRuntime
{
    public TimelineSelectionRuntime(
        ObservableCollection<TrackViewModel> tracks,
        NoteSelectionPanelViewModel noteSelectionPanel,
        Action<TimelineSelectionContext> setSelectionContext,
        Action notifyClipboardStateChanged,
        Action<TrackViewModel?, NoteViewModel?> refreshNoteSelectionState,
        Func<bool> hasSelectedEditableKeyframes)
    {
        Tracks = tracks;
        NoteSelectionPanel = noteSelectionPanel;
        SetSelectionContext = setSelectionContext;
        NotifyClipboardStateChanged = notifyClipboardStateChanged;
        RefreshNoteSelectionState = refreshNoteSelectionState;
        HasSelectedEditableKeyframes = hasSelectedEditableKeyframes;
    }

    public ObservableCollection<TrackViewModel> Tracks { get; }

    public NoteSelectionPanelViewModel NoteSelectionPanel { get; }

    public Action<TimelineSelectionContext> SetSelectionContext { get; }

    public Action NotifyClipboardStateChanged { get; }

    public Action<TrackViewModel?, NoteViewModel?> RefreshNoteSelectionState { get; }

    public Func<bool> HasSelectedEditableKeyframes { get; }
}
