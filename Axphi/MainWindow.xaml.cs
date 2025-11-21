using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SaveFileDialog? _saveChartDialog;

    private MediaFoundationReader? _musicReader;
    private WasapiOut? _wasapiOut;

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(31, 31, 31);
        base.OnSourceInitialized(e);
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

    [RelayCommand]
    private void MinimizeSelf()
        => WindowState = WindowState.Minimized;

    [RelayCommand]
    private void MaximizeRestoreSelf() => WindowState = WindowState switch
    {
        WindowState.Maximized => WindowState.Normal,
        _ => WindowState.Maximized
    };

    [RelayCommand]
    private void CloseSelf() 
        => Close();

    private void RenderTimerCallback(object? sender, EventArgs e)
    {
        _renderStopwatch ??= new Stopwatch();
        chartRenderer.Time = _renderStopwatch.Elapsed;
    }
}