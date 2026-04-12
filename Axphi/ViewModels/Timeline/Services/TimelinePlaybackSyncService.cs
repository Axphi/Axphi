using Axphi.Data;
using Axphi.Data.KeyFrames;
using System.Collections.Generic;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelinePlaybackSyncService : ITimelinePlaybackSyncService
{
    public void SyncTrackValuesToTime(
        int currentTick,
        KeyFrameEasingDirection easingDirection,
        BpmTrackViewModel? bpmTrack,
        IEnumerable<TrackViewModel> tracks,
        TrackViewModel? activeNotePanelOwner,
        TrackViewModel? editorActiveTrack,
        double viewportStartTick,
        double viewportEndTick)
    {
        bpmTrack?.SyncValuesToTime(currentTick);

        var allTracks = tracks as IList<TrackViewModel> ?? tracks.ToList();
        var syncScope = BuildSyncScope(allTracks, activeNotePanelOwner, editorActiveTrack, viewportStartTick, viewportEndTick);

        foreach (var track in syncScope)
        {
            track.SyncValuesToTime(currentTick, easingDirection);

            // During playback, only note rows currently in edit/focus context need live value mirroring.
            if (ReferenceEquals(track, editorActiveTrack))
            {
                foreach (var note in track.UINotes)
                {
                    note.SyncValuesToTime(currentTick, easingDirection);
                }
                continue;
            }

            if (ReferenceEquals(track, activeNotePanelOwner) && track.SelectedNote != null)
            {
                track.SelectedNote.SyncValuesToTime(currentTick, easingDirection);
            }

            foreach (var note in track.UINotes.Where(static note => note.IsSelected))
            {
                note.SyncValuesToTime(currentTick, easingDirection);
            }
        }
    }

    private static HashSet<TrackViewModel> BuildSyncScope(
        IEnumerable<TrackViewModel> tracks,
        TrackViewModel? activeNotePanelOwner,
        TrackViewModel? editorActiveTrack,
        double viewportStartTick,
        double viewportEndTick)
    {
        var scope = new HashSet<TrackViewModel>();
        double minViewportTick = System.Math.Min(viewportStartTick, viewportEndTick);
        double maxViewportTick = System.Math.Max(viewportStartTick, viewportEndTick);
        bool hasViewport = maxViewportTick > minViewportTick;

        if (activeNotePanelOwner != null)
        {
            scope.Add(activeNotePanelOwner);
        }

        if (editorActiveTrack != null)
        {
            scope.Add(editorActiveTrack);
        }

        foreach (var track in tracks)
        {
            bool isInteractionPinned = track.IsLayerSelected || track.HasSelectedChildren || track.IsNotePanelOwner || track.SelectedNote != null;

            if (isInteractionPinned)
            {
                scope.Add(track);
                continue;
            }

            if (!hasViewport)
            {
                scope.Add(track);
                continue;
            }

            double trackStartTick = track.Data.StartTick;
            double trackEndTick = track.Data.StartTick + System.Math.Max(1, track.Data.DurationTicks);
            bool intersectsViewport = trackEndTick >= minViewportTick && trackStartTick <= maxViewportTick;
            if (intersectsViewport)
            {
                scope.Add(track);
            }
        }

        return scope;
    }
}
