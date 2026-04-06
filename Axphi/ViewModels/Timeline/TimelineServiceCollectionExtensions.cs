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
        services.AddSingleton<ITimelineSelectionService, TimelineSelectionService>();
        services.AddSingleton<ITimelineMutationSyncService, TimelineMutationSyncService>();
        services.AddSingleton<ITimelinePlaybackSyncService, TimelinePlaybackSyncService>();
        services.AddSingleton<ITimelineStateService, TimelineStateService>();
        services.AddSingleton<ITimelineWorkspaceLoopService, TimelineWorkspaceLoopService>();

        services.AddSingleton<ITimelineTrackFactory, TimelineTrackFactory>();
        services.AddSingleton<ITimelineHistoryCoordinator, TimelineHistoryCoordinator>();

        return services;
    }
}
