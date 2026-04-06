using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineTrackHierarchyService : ITimelineTrackHierarchyService
{
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
