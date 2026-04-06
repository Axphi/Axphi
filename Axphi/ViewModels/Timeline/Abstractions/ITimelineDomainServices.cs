namespace Axphi.ViewModels;

public interface ITimelineDomainServices
{
    ITimelineSelectionService Selection { get; }
    ITimelineTrackHierarchyService TrackHierarchy { get; }
    ITimelineDeletionService Deletion { get; }
    ITimelineSnapService Snap { get; }
    ITimelineClipboardSelectionService ClipboardSelection { get; }
    ITimelineMutationSyncService MutationSync { get; }
    ITimelineClipboardCloneService ClipboardClone { get; }
    ITimelineClipboardCollectorService ClipboardCollector { get; }
    ITimelineClipboardPasteService ClipboardPaste { get; }
    ITimelineSnapshotService Snapshot { get; }
    ITimelineUiStateService UiState { get; }
    ITimelineUiRestoreService UiRestore { get; }
    ITimelinePlaybackRestoreService PlaybackRestore { get; }
    ITimelineWorkspaceLoopService WorkspaceLoop { get; }
    ITimelineTrackMaterializerService TrackMaterializer { get; }
}
