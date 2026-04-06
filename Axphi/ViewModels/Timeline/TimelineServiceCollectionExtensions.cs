using Microsoft.Extensions.DependencyInjection;

namespace Axphi.ViewModels;

public static class TimelineServiceCollectionExtensions
{
    public static IServiceCollection AddTimelineServices(this IServiceCollection services)
    {
        services.AddSingleton<TimelineViewModel>();

        services.AddSingleton<ITimelineSelectionService, TimelineSelectionService>();
        services.AddSingleton<ITimelineTrackHierarchyService, TimelineTrackHierarchyService>();
        services.AddSingleton<ITimelineDeletionService, TimelineDeletionService>();
        services.AddSingleton<ITimelineSnapService, TimelineSnapService>();
        services.AddSingleton<ITimelineClipboardSelectionService, TimelineClipboardSelectionService>();
        services.AddSingleton<ITimelineMutationSyncService, TimelineMutationSyncService>();
        services.AddSingleton<ITimelineClipboardCloneService, TimelineClipboardCloneService>();
        services.AddSingleton<ITimelineClipboardCollectorService, TimelineClipboardCollectorService>();
        services.AddSingleton<ITimelineClipboardPasteService, TimelineClipboardPasteService>();
        services.AddSingleton<ITimelineSnapshotService, TimelineSnapshotService>();
        services.AddSingleton<ITimelineUiStateService, TimelineUiStateService>();
        services.AddSingleton<ITimelineUiRestoreService, TimelineUiRestoreService>();
        services.AddSingleton<ITimelinePlaybackRestoreService, TimelinePlaybackRestoreService>();
        services.AddSingleton<ITimelineWorkspaceLoopService, TimelineWorkspaceLoopService>();
        services.AddSingleton<ITimelineTrackMaterializerService, TimelineTrackMaterializerService>();
        services.AddSingleton<ITimelineDomainServices, TimelineDomainServices>();

        services.AddSingleton<ITimelineTrackFactory, TimelineTrackFactory>();
        services.AddSingleton<ITimelineHistoryCoordinator, TimelineHistoryCoordinator>();

        return services;
    }
}
