namespace Axphi.ViewModels;

public sealed class TimelineDomainServices : ITimelineDomainServices
{
    public TimelineDomainServices(
        ITimelineEditingService editing,
        ITimelineSnapService snap,
        ITimelineClipboardService clipboard,
        ITimelineMutationSyncService mutationSync,
        ITimelineStateService state,
        ITimelineWorkspaceLoopService workspaceLoop,
        ITimelineTrackMaterializerService trackMaterializer)
    {
        Editing = editing;
        Snap = snap;
        Clipboard = clipboard;
        MutationSync = mutationSync;
        State = state;
        WorkspaceLoop = workspaceLoop;
        TrackMaterializer = trackMaterializer;
    }

    public ITimelineEditingService Editing { get; }
    public ITimelineSnapService Snap { get; }
    public ITimelineClipboardService Clipboard { get; }
    public ITimelineMutationSyncService MutationSync { get; }
    public ITimelineStateService State { get; }
    public ITimelineWorkspaceLoopService WorkspaceLoop { get; }
    public ITimelineTrackMaterializerService TrackMaterializer { get; }
}
