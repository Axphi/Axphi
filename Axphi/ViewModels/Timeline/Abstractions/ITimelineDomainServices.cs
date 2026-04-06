namespace Axphi.ViewModels;

public interface ITimelineDomainServices
{
    ITimelineEditingService Editing { get; }
    ITimelineSnapService Snap { get; }
    ITimelineClipboardService Clipboard { get; }
    ITimelineMutationSyncService MutationSync { get; }
    ITimelineStateService State { get; }
    ITimelineWorkspaceLoopService WorkspaceLoop { get; }
    ITimelineTrackMaterializerService TrackMaterializer { get; }
}
