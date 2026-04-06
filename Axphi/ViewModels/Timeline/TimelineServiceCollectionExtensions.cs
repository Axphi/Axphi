using Microsoft.Extensions.DependencyInjection;

namespace Axphi.ViewModels;

public static class TimelineServiceCollectionExtensions
{
    public static IServiceCollection AddTimelineServices(this IServiceCollection services)
    {
        services.AddSingleton<TimelineViewModel>();

        services.AddSingleton<ITimelineEditingService, TimelineEditingService>();
        services.AddSingleton<ITimelineSnapService, TimelineSnapService>();
        services.AddSingleton<ITimelineClipboardService, TimelineClipboardService>();
        services.AddSingleton<ITimelineKeyframeSelectionService, TimelineKeyframeSelectionService>();
        services.AddSingleton<ITimelineNoteSelectionService, TimelineNoteSelectionService>();
        services.AddSingleton<ITimelineMutationSyncService, TimelineMutationSyncService>();
        services.AddSingleton<ITimelineSnapshotService, TimelineSnapshotService>();
        services.AddSingleton<ITimelineUiStateService, TimelineUiStateService>();
        services.AddSingleton<ITimelineUiRestoreService, TimelineUiRestoreService>();
        services.AddSingleton<ITimelinePlaybackRestoreService, TimelinePlaybackRestoreService>();
        services.AddSingleton<ITimelinePlaybackSyncService, TimelinePlaybackSyncService>();
        services.AddSingleton<ITimelineStateService, TimelineStateService>();
        services.AddSingleton<ITimelineWorkspaceLoopService, TimelineWorkspaceLoopService>();
        services.AddSingleton<ITimelineTrackMaterializerService, TimelineTrackMaterializerService>();
        services.AddSingleton<ITimelineDomainServices, TimelineDomainServices>();

        services.AddSingleton<ITimelineTrackFactory, TimelineTrackFactory>();
        services.AddSingleton<ITimelineHistoryCoordinator, TimelineHistoryCoordinator>();

        return services;
    }
}
