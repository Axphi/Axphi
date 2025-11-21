using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Utils;
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
    private OpenFileDialog? _importMusicDialog;

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
            _wasapiOut?.Play();
            _renderStopwatch.Start();
        }
        else
        {
            _wasapiOut?.Pause();
            _renderStopwatch.Stop();
        }
    }

    [RelayCommand]
    private void StopChartRendering()
    {
        _dispatcherTimer?.Stop();
        _renderStopwatch?.Stop();
        _renderStopwatch?.Reset();
        _wasapiOut?.Stop();

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
    private void ImportMusic()
    {
        _importMusicDialog ??= new OpenFileDialog()
        {
            Title = "Import music",
            Filter = "Audio file|*.mp3;*.ogg;*.wav|Any|*.*",
            CheckFileExists = true,
        };

        if (_importMusicDialog.ShowDialog(this) != true)
        {
            return;
        }

        ProjectManager.EditingProject.EncodedAudio = System.IO.File.ReadAllBytes(_importMusicDialog.FileName);
        _musicReader = new MediaFoundationReader(_importMusicDialog.FileName);
        _wasapiOut ??= new WasapiOut();
        _wasapiOut.Init(_musicReader);
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
        if (_wasapiOut is not null &&
            _wasapiOut.PlaybackState == PlaybackState.Playing)
        {
            chartRenderer.Time = _wasapiOut.GetPositionTimeSpan();
            return;
        }

        _renderStopwatch ??= new Stopwatch();
        chartRenderer.Time = _renderStopwatch.Elapsed;
    }
}