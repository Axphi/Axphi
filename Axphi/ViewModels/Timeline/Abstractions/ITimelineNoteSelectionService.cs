using System.Collections.Generic;

namespace Axphi.ViewModels;

public interface ITimelineNoteSelectionService
{
    TrackViewModel? RefreshSelection(
        IEnumerable<TrackViewModel> tracks,
        TrackViewModel? activeOwner,
        NoteSelectionPanelViewModel panel,
        TrackViewModel? preferredOwner = null,
        NoteViewModel? preferredSingle = null);
}
