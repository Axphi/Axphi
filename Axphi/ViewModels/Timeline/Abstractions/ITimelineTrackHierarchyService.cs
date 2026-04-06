using System;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels;

public interface ITimelineTrackHierarchyService
{
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
