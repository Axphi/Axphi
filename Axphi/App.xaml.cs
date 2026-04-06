using Axphi.Services;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Axphi;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; } = BuildServiceProvider();

    private static IServiceProvider BuildServiceProvider()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton<MainWindow>();

        serviceCollection.AddSingleton<MainViewModel>();

        serviceCollection.AddSingleton<BezierViewModel>();

        serviceCollection.AddSingleton<ProjectManager>();

        serviceCollection.AddSingleton<IFileService, WindowsFileService>();

        serviceCollection.AddSingleton<FileActionsViewModel>();

        serviceCollection.AddSingleton<TimelineViewModel>();
        serviceCollection.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        serviceCollection.AddSingleton<ITimelineTrackFactory, TimelineTrackFactory>();
        serviceCollection.AddSingleton<ITimelineHistoryCoordinator, TimelineHistoryCoordinator>();

        



        return serviceCollection.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}

