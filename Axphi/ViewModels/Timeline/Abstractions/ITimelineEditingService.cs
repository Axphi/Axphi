using System;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public interface ITimelineEditingService
{
    void ClearKeyframeSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void ClearLayerSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void ClearNoteSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void ClearAllSelections(TimelineSelectionRuntime runtime);

    void EnterLayerSelectionContext(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void EnterSubItemSelectionContext(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void RefreshLayerSelectionVisuals(TimelineSelectionRuntime runtime);

    bool DeleteSelected(TimelineDeleteRuntime runtime);

    void ReindexTrackNames(ObservableCollection<TrackViewModel> tracks);

    bool TrySetParentLine(
        ObservableCollection<TrackViewModel> tracks,
        TrackViewModel childTrack,
        string? parentLineId,
        Action onHierarchyChanged);

    void RefreshParentLineBindings(
        ObservableCollection<TrackViewModel> tracks,
        Action onHierarchyChanged);
}
