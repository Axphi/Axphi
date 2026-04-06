using Axphi.Data;
using Axphi.Data.KeyFrames;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineDeletionService : ITimelineDeletionService
{
    public bool DeleteSelected(TimelineDeleteRuntime runtime)
    {
        var layersToSelectAfterDelete = new HashSet<TrackViewModel>();
        int deletedChildCount = 0;

        if (runtime.BpmTrack != null)
        {
            deletedChildCount += RemoveSelectedKeyframes(runtime.CurrentChart.BpmKeyFrames, runtime.BpmTrack.UIBpmKeyframes);
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

    private static int DeleteSelectedChildrenInTrack(TrackViewModel track)
    {
        int deletedCount = 0;

        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Anchor.KeyFrames, track.UIAnchorKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Offset.KeyFrames, track.UIOffsetKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Scale.KeyFrames, track.UIScaleKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Rotation.KeyFrames, track.UIRotationKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.AnimatableProperties.Opacity.KeyFrames, track.UIOpacityKeyframes);
        deletedCount += RemoveSelectedKeyframes(track.Data.SpeedKeyFrames, track.UISpeedKeyframes);

        foreach (var note in track.UINotes)
        {
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Anchor.KeyFrames, note.UIAnchorKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Offset.KeyFrames, note.UIOffsetKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Scale.KeyFrames, note.UIScaleKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Rotation.KeyFrames, note.UIRotationKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.AnimatableProperties.Opacity.KeyFrames, note.UIOpacityKeyframes);
            deletedCount += RemoveSelectedKeyframes(note.Model.KindKeyFrames, note.UINoteKindKeyframes);
        }

        var notesToDelete = track.UINotes.Where(note => note.IsSelected).ToList();
        foreach (var note in notesToDelete)
        {
            track.Data.Notes.Remove(note.Model);
            track.UINotes.Remove(note);
            deletedCount++;

            if (track.SelectedNote == note)
            {
                track.SelectedNote = null;
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
            uiList.Remove(wrapper);
        }

        return wrappersToDelete.Count;
    }
}
