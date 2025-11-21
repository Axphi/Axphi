using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SaveFileDialog? _saveChartDialog;

    private DispatcherTimer? _dispatcherTimer;
    private Stopwatch? _renderStopwatch;

    public MainViewModel ViewModel { get; }
    public ProjectManager ProjectManager { get; }

    public MainWindow(
        MainViewModel viewModel,
        ProjectManager projectManager)
    {
        ViewModel = viewModel;
        ProjectManager = projectManager;
        DataContext = this;
        InitializeComponent();
    }

    [RelayCommand]
    private void PlayPauseChartRendering()
    {
        _renderStopwatch ??= new Stopwatch();
        if (_dispatcherTimer is null)
        {
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Render, RenderTimerCallback, Dispatcher);
        }
        else
        {
            _dispatcherTimer.IsEnabled ^= true;
        }

        if (_dispatcherTimer.IsEnabled)
        {
            _renderStopwatch.Start();
        }
        else
        {
            _renderStopwatch.Stop();
        }
    }

    [RelayCommand]
    private void StopChartRendering()
    {
        _renderStopwatch?.Stop();
        _renderStopwatch?.Reset();
        _dispatcherTimer?.Stop();

        chartRenderer.Time = default;
    }

    [RelayCommand]
    private void LoadDemoChart()
    {
        ProjectManager.EditingProject = new Project()
        {
            Chart = DebuggingUtils.CreateDemoChart()
        };
        ProjectManager.EditingProjectFilePath = null;
    }

    [RelayCommand]
    private void SaveChart()
    {
        if (ProjectManager.EditingProject is null)
        {
            return;
        }

        if (ProjectManager.EditingProjectFilePath is null)
        {
            _saveChartDialog ??= new SaveFileDialog()
            {
                Title = "Save Chart",
                FileName = "New Axphi Project",
                Filter = "Axphi Project|*.axp|Any File|*.*",
                CheckPathExists = true,
            };

            if (_saveChartDialog.ShowDialog(this) != true)
            {
                return;
            }

            ProjectManager.EditingProjectFilePath = _saveChartDialog.FileName;
        }

        ProjectManager.SaveEditingProject(ProjectManager.EditingProjectFilePath);
    }

    private void RenderTimerCallback(object? sender, EventArgs e)
    {
        _renderStopwatch ??= new Stopwatch();
        chartRenderer.Time = _renderStopwatch.Elapsed;
    }
}