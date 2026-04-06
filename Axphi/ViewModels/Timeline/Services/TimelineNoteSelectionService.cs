using System;
using System.Collections.Generic;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineNoteSelectionService : ITimelineNoteSelectionService
{
    public TrackViewModel? RefreshSelection(
        IEnumerable<TrackViewModel> tracks,
        TrackViewModel? activeOwner,
        NoteSelectionPanelViewModel panel,
        TrackViewModel? preferredOwner = null,
        NoteViewModel? preferredSingle = null)
    {
        var trackList = tracks.ToList();
        var selectedEntries = trackList
            .SelectMany(track => track.UINotes.Where(note => note.IsSelected).Select(note => (track, note)))
            .ToList();

        foreach (var track in trackList)
        {
            track.SelectedNote = null;
            track.IsNotePanelOwner = false;
        }

        if (selectedEntries.Count == 0)
        {
            var owner = preferredOwner ?? activeOwner;
            if (owner != null)
            {
                owner.IsNotePanelOwner = true;
            }

            panel.SyncSelection(Array.Empty<NoteViewModel>());
            return owner;
        }

        TrackViewModel ownerTrack;
        if (preferredOwner != null && selectedEntries.Any(entry => ReferenceEquals(entry.track, preferredOwner)))
        {
            ownerTrack = preferredOwner;
        }
        else if (activeOwner != null && selectedEntries.Any(entry => ReferenceEquals(entry.track, activeOwner)))
        {
            ownerTrack = activeOwner;
        }
        else
        {
            ownerTrack = selectedEntries[0].track;
        }

        ownerTrack.IsNotePanelOwner = true;

        if (selectedEntries.Count == 1)
        {
            var selectedNote = preferredSingle != null && selectedEntries.Any(entry => ReferenceEquals(entry.note, preferredSingle))
                ? preferredSingle
                : selectedEntries[0].note;
            ownerTrack = selectedEntries.First(entry => ReferenceEquals(entry.note, selectedNote)).track;
            ownerTrack.IsNotePanelOwner = true;
            ownerTrack.SelectedNote = selectedNote;
        }

        panel.SyncSelection(selectedEntries.Select(entry => entry.note).ToList());
        return ownerTrack;
    }
}
