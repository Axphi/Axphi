namespace Axphi.ViewModels;

public sealed class TimelineDomainServices : ITimelineDomainServices
{
    public TimelineDomainServices(
        ITimelineSelectionService selection,
        ITimelineTrackHierarchyService trackHierarchy,
        ITimelineDeletionService deletion,
        ITimelineSnapService snap,
        ITimelineClipboardSelectionService clipboardSelection,
        ITimelineMutationSyncService mutationSync,
        ITimelineClipboardCloneService clipboardClone,
        ITimelineClipboardCollectorService clipboardCollector,
        ITimelineClipboardPasteService clipboardPaste,
        ITimelineSnapshotService snapshot,
        ITimelineUiStateService uiState,
        ITimelineUiRestoreService uiRestore,
        ITimelinePlaybackRestoreService playbackRestore,
        ITimelineWorkspaceLoopService workspaceLoop,
        ITimelineTrackMaterializerService trackMaterializer)
    {
        Selection = selection;
        TrackHierarchy = trackHierarchy;
        Deletion = deletion;
        Snap = snap;
        ClipboardSelection = clipboardSelection;
        MutationSync = mutationSync;
        ClipboardClone = clipboardClone;
        ClipboardCollector = clipboardCollector;
        ClipboardPaste = clipboardPaste;
        Snapshot = snapshot;
        UiState = uiState;
        UiRestore = uiRestore;
        PlaybackRestore = playbackRestore;
        WorkspaceLoop = workspaceLoop;
        TrackMaterializer = trackMaterializer;
    }

    public ITimelineSelectionService Selection { get; }
    public ITimelineTrackHierarchyService TrackHierarchy { get; }
    public ITimelineDeletionService Deletion { get; }
    public ITimelineSnapService Snap { get; }
    public ITimelineClipboardSelectionService ClipboardSelection { get; }
    public ITimelineMutationSyncService MutationSync { get; }
    public ITimelineClipboardCloneService ClipboardClone { get; }
    public ITimelineClipboardCollectorService ClipboardCollector { get; }
    public ITimelineClipboardPasteService ClipboardPaste { get; }
    public ITimelineSnapshotService Snapshot { get; }
    public ITimelineUiStateService UiState { get; }
    public ITimelineUiRestoreService UiRestore { get; }
    public ITimelinePlaybackRestoreService PlaybackRestore { get; }
    public ITimelineWorkspaceLoopService WorkspaceLoop { get; }
    public ITimelineTrackMaterializerService TrackMaterializer { get; }
}
