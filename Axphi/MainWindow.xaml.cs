using Axphi.Data;
using Axphi.Playback;
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
    private WasapiOutBasedPlayTimeSyncProvider _customPlayTimeSyncProvider;

    public MainViewModel ViewModel { get; }
    public ProjectManager ProjectManager { get; }
    public PlaybackService PlaybackService { get; }

    public MainWindow(
        MainViewModel viewModel,
        ProjectManager projectManager,
        PlaybackService playbackService)
    {
        ViewModel = viewModel;
        ProjectManager = projectManager;
        PlaybackService = playbackService;
        DataContext = this;
        InitializeComponent();

        playbackService.ChartRenderer = chartRenderer;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(31, 31, 31);
        base.OnSourceInitialized(e);
    }

    private void EnsurePlayTimeSyncProvider()
    {
        if (_wasapiOut is null)
        {
            PlaybackService.CustomPlayTimeSyncProvider = null;
        }
        else if (_customPlayTimeSyncProvider is not null)
        {
            PlaybackService.CustomPlayTimeSyncProvider = _customPlayTimeSyncProvider;
        }
    }

    [RelayCommand]
    private void PlayPauseChartRendering()
    {
        EnsurePlayTimeSyncProvider();

        if (PlaybackService.IsPlaying)
        {
            PlaybackService.Pause();
        }
        else
        {
            PlaybackService.Play();
        }
    }

    [RelayCommand]
    private void StopChartRendering()
    {
        EnsurePlayTimeSyncProvider();
        PlaybackService.Stop();
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
        _customPlayTimeSyncProvider = new WasapiOutBasedPlayTimeSyncProvider(_musicReader, _wasapiOut);
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
}