using CommunityToolkit.Mvvm.Messaging;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineSelectionService : ITimelineSelectionService
{
    private readonly IMessenger _messenger;

    public TimelineSelectionService(IMessenger messenger)
    {
        _messenger = messenger;
    }

    public void ClearKeyframeSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null)
    {
        _messenger.Send(new ClearSelectionMessage(SelectionGroup.Keyframes, senderToIgnore));
        RefreshLayerSelectionVisuals(runtime);
    }

    public void ClearLayerSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null)
    {
        _messenger.Send(new ClearSelectionMessage(SelectionGroup.Layers, senderToIgnore));
        RefreshLayerSelectionVisuals(runtime);
    }

    public void ClearNoteSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null)
    {
        _messenger.Send(new ClearSelectionMessage(SelectionGroup.Notes, senderToIgnore));

        foreach (var track in runtime.Tracks)
        {
            track.SelectedNote = null;
        }

        runtime.RefreshNoteSelectionState(null, null);
        RefreshLayerSelectionVisuals(runtime);
    }

    public void ClearAllSelections(TimelineSelectionRuntime runtime)
    {
        ClearKeyframeSelection(runtime);
        ClearNoteSelection(runtime);
        ClearLayerSelection(runtime);
        runtime.SetSelectionContext(TimelineSelectionContext.None);
    }

    public void EnterLayerSelectionContext(TimelineSelectionRuntime runtime, object? senderToIgnore = null)
    {
        ClearKeyframeSelection(runtime, senderToIgnore);
        ClearNoteSelection(runtime, senderToIgnore);
        runtime.SetSelectionContext(TimelineSelectionContext.Layers);
    }

    public void EnterSubItemSelectionContext(TimelineSelectionRuntime runtime, object? senderToIgnore = null)
    {
        ClearLayerSelection(runtime, senderToIgnore);
        runtime.SetSelectionContext(TimelineSelectionContext.SubItems);
    }

    public void RefreshLayerSelectionVisuals(TimelineSelectionRuntime runtime)
    {
        foreach (var track in runtime.Tracks)
        {
            track.HasSelectedChildren = TrackHasSelectedChildren(track);
        }

        runtime.NotifyClipboardStateChanged();
    }

    private static bool TrackHasSelectedChildren(TrackViewModel track)
    {
        if (track.UIAnchorKeyframes.Any(keyframe => keyframe.IsSelected)
            || track.UIOffsetKeyframes.Any(keyframe => keyframe.IsSelected)
            || track.UIScaleKeyframes.Any(keyframe => keyframe.IsSelected)
            || track.UIRotationKeyframes.Any(keyframe => keyframe.IsSelected)
            || track.UIOpacityKeyframes.Any(keyframe => keyframe.IsSelected)
            || track.UISpeedKeyframes.Any(keyframe => keyframe.IsSelected))
        {
            return true;
        }

        return track.UINotes.Any(note =>
            note.IsSelected
            || note.UIAnchorKeyframes.Any(keyframe => keyframe.IsSelected)
            || note.UIOffsetKeyframes.Any(keyframe => keyframe.IsSelected)
            || note.UIScaleKeyframes.Any(keyframe => keyframe.IsSelected)
            || note.UIRotationKeyframes.Any(keyframe => keyframe.IsSelected)
            || note.UIOpacityKeyframes.Any(keyframe => keyframe.IsSelected)
            || note.UINoteKindKeyframes.Any(keyframe => keyframe.IsSelected));
    }
}
