using Axphi.Services;
using Axphi.ViewModels;
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

        return serviceCollection.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}

