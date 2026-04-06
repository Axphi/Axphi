using Axphi.Data;
using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineEditingService : ITimelineEditingService
{
    private readonly IMessenger _messenger;

    public TimelineEditingService(IMessenger messenger)
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

    public bool DeleteSelected(TimelineDeleteRuntime runtime)
    {
        var layersToSelectAfterDelete = new HashSet<TrackViewModel>();
        int deletedChildCount = 0;

        if (runtime.BpmTrack != null)
        {
            deletedChildCount += RemoveSelectedKeyframes(runtime.CurrentChart.BpmKeyFrames, runtime.BpmTrack.UIBpmKeyframes);
            runtime.BpmTrack.SyncBpmKeyframeProjection();
        }

        foreach (var track in runtime.Tracks)
        {
            int deletedInTrack = DeleteSelectedChildrenInTrack(track);
            if (deletedInTrack > 0)
            {
                deletedChildCount += deletedInTrack;
                layersToSelectAfterDelete.Add(track);
            }
        }

        if (deletedChildCount > 0)
        {
            foreach (var track in layersToSelectAfterDelete)
            {
                track.IsLayerSelected = true;
            }

            runtime.SetSelectionContext(TimelineSelectionContext.Layers);
            return true;
        }

        bool hasDeletedLayers = false;

        if (runtime.AudioTrack?.IsLayerSelected == true)
        {
            runtime.AudioTrack.DeleteAudio();
            hasDeletedLayers = true;
        }

        var tracksToDelete = runtime.Tracks.Where(track => track.IsLayerSelected).ToList();
        foreach (var track in tracksToDelete)
        {
            runtime.CurrentChart.JudgementLines.Remove(track.Data);
            runtime.Tracks.Remove(track);
            hasDeletedLayers = true;
        }

        if (!hasDeletedLayers)
        {
            return false;
        }

        runtime.SetSelectionContext(TimelineSelectionContext.None);
        runtime.ReindexTrackNames();
        runtime.RefreshParentLineBindings();
        return true;
    }

    public void ReindexTrackNames(ObservableCollection<TrackViewModel> tracks)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            tracks[i].TrackName = $"判定线图层 {i + 1}";
        }

        foreach (var track in tracks)
        {
            track.NotifyParentBindingChanged();
        }
    }

    public bool TrySetParentLine(
        ObservableCollection<TrackViewModel> tracks,
        TrackViewModel childTrack,
        string? parentLineId,
        Action onHierarchyChanged)
    {
        if (!tracks.Contains(childTrack))
        {
            return false;
        }

        string? normalizedParentId = string.IsNullOrWhiteSpace(parentLineId) ? null : parentLineId;
        if (childTrack.Data.ParentLineId == normalizedParentId)
        {
            return true;
        }

        if (normalizedParentId != null)
        {
            var parentTrack = tracks.FirstOrDefault(track => track.Data.ID == normalizedParentId);
            if (parentTrack == null || ReferenceEquals(parentTrack, childTrack))
            {
                return false;
            }

            if (WillCreateParentCycle(tracks, childTrack.Data.ID, normalizedParentId))
            {
                return false;
            }
        }

        childTrack.ApplyParentLineId(normalizedParentId);
        onHierarchyChanged();
        return true;
    }

    public void RefreshParentLineBindings(
        ObservableCollection<TrackViewModel> tracks,
        Action onHierarchyChanged)
    {
        var validIds = tracks.Select(track => track.Data.ID).ToHashSet();
        bool changed = false;

        foreach (var track in tracks)
        {
            if (!string.IsNullOrWhiteSpace(track.Data.ParentLineId) && !validIds.Contains(track.Data.ParentLineId))
            {
                track.ApplyParentLineId(null);
                changed = true;
            }

            track.NotifyParentBindingChanged();
        }

        if (changed)
        {
            onHierarchyChanged();
        }
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

    private static int DeleteSelectedChildrenInTrack(TrackViewModel track)
    {
        int deletedCount = 0;

        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Anchor.KeyFrames, track.UIAnchorKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Offset.KeyFrames, track.UIOffsetKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Scale.KeyFrames, track.UIScaleKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Rotation.KeyFrames, track.UIRotationKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Opacity.KeyFrames, track.UIOpacityKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.SpeedKeyFrames, track.UISpeedKeyframes);
        track.SyncAllTrackKeyframeProjections();

        foreach (var note in track.UINotes)
        {
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Anchor.KeyFrames, note.UIAnchorKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Offset.KeyFrames, note.UIOffsetKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Scale.KeyFrames, note.UIScaleKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Rotation.KeyFrames, note.UIRotationKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Opacity.KeyFrames, note.UIOpacityKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.KindKeyFrames, note.UINoteKindKeyframes);
            note.SyncAllKeyframeProjections();
        }

        var notesToDelete = track.UINotes.Where(note => note.IsSelected).ToList();
        foreach (var note in notesToDelete)
        {
            if (track.RemoveNoteModel(note))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private static int RemoveSelectedKeyframes<T, TKeyFrame>(
        List<TKeyFrame>? dataList,
        ObservableCollection<KeyFrameUIWrapper<T>> uiList)
        where T : struct
        where TKeyFrame : KeyFrame<T>
    {
        if (dataList == null || uiList.Count == 0)
        {
            return 0;
        }

        var wrappersToDelete = uiList.Where(wrapper => wrapper.IsSelected).ToList();
        foreach (var wrapper in wrappersToDelete)
        {
            dataList.Remove((TKeyFrame)wrapper.Model);
        }

        return wrappersToDelete.Count;
    }

    private static bool WillCreateParentCycle(
        ObservableCollection<TrackViewModel> tracks,
        string childLineId,
        string candidateParentId)
    {
        string? current = candidateParentId;
        var visited = new HashSet<string> { childLineId };

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (!visited.Add(current))
            {
                return true;
            }

            var next = tracks.FirstOrDefault(track => track.Data.ID == current)?.Data.ParentLineId;
            current = string.IsNullOrWhiteSpace(next) ? null : next;
        }

        return false;
    }
}
