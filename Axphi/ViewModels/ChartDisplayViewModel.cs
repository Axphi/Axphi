using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace Axphi.ViewModels;

public partial class ChartDisplayViewModel : ObservableObject, IDisposable
{
    private readonly ProjectManager _projectManager;
    private readonly IMessenger _messenger;
    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly PlaybackPositionService _playbackPositionService;
    private DispatcherTimer? _dispatcherTimer;
    private Stopwatch? _renderStopwatch;
    private TimeSpan _manualTimeOffset = TimeSpan.Zero;
    private bool _disposed;

    [ObservableProperty]
    private Chart? _currentChart;

    [ObservableProperty]
    private byte[]? _illustrationBytes;

    [ObservableProperty]
    private TimeSpan _currentTime;

    [ObservableProperty]
    private bool _showAuxiliaryUi;

    [ObservableProperty]
    private bool _showNoteCenters;

    [ObservableProperty]
    private double _backgroundDimOpacity = 0.3;

    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    [ObservableProperty]
    private bool _preserveAudioPitch = true;

    public bool IsPlaying => _dispatcherTimer?.IsEnabled == true;

    public event EventHandler? RenderInvalidated;
    public event EventHandler<string>? UserNotificationRequested;

    public ChartDisplayViewModel(
        ProjectManager projectManager,
        IMessenger messenger,
        IAudioPlaybackService audioPlaybackService,
        PlaybackPositionService playbackPositionService)
    {
        _projectManager = projectManager;
        _messenger = messenger;
        _audioPlaybackService = audioPlaybackService;
        _playbackPositionService = playbackPositionService;

        RefreshProjectBindings();

        _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;

        _messenger.Register<UpdateRendererMessage>(this, (_, _) => InvalidateRender());
        _messenger.Register<AudioLoadedMessage>(this, (_, message) => LoadAudio(message.FilePath));
        _messenger.Register<IllustrationLoadedMessage>(this, (_, _) =>
        {
            IllustrationBytes = _projectManager.EditingProject?.EncodedIllustration;
            InvalidateRender();
        });
    }

    partial void OnCurrentTimeChanged(TimeSpan value)
    {
        UpdatePlaybackPosition(value);
        InvalidateRender();
    }

    partial void OnPlaybackSpeedChanged(double value)
    {
        double clamped = Math.Clamp(value, 0.1, 4.0);
        if (Math.Abs(clamped - value) > 0.000001)
        {
            PlaybackSpeed = clamped;
            return;
        }

        ApplyAudioSpeedSettings();
    }

    partial void OnPreserveAudioPitchChanged(bool value)
    {
        ApplyAudioSpeedSettings();
    }

    partial void OnBackgroundDimOpacityChanged(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(clamped - value) > 0.000001)
        {
            BackgroundDimOpacity = clamped;
        }
    }

    [RelayCommand]
    private void PlayPauseChartRendering()
    {
        _renderStopwatch ??= new Stopwatch();
        if (_dispatcherTimer is null)
        {
            _dispatcherTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _dispatcherTimer.Tick += RenderTimerCallback;
            _dispatcherTimer.Start();
        }
        else
        {
            _dispatcherTimer.IsEnabled ^= true;
        }

        if (_dispatcherTimer.IsEnabled)
        {
            ApplyAudioSpeedSettings();
            _audioPlaybackService.StartAtTimelineTime(_manualTimeOffset);
            _renderStopwatch.Start();
        }
        else
        {
            _audioPlaybackService.Pause();
            _renderStopwatch.Stop();
            _manualTimeOffset = CurrentTime;
            SnapToNearestTick();
        }

        OnPropertyChanged(nameof(IsPlaying));
    }

    [RelayCommand]
    private void StopChartRendering()
    {
        _dispatcherTimer?.Stop();
        _renderStopwatch?.Stop();
        _renderStopwatch?.Reset();
        _audioPlaybackService.StopAndReset();

        _manualTimeOffset = TimeSpan.Zero;
        CurrentTime = default;
        _playbackPositionService.Reset();
        OnPropertyChanged(nameof(IsPlaying));
    }

    public void SeekTo(TimeSpan time)
    {
        _audioPlaybackService.SeekTo(time);
        _manualTimeOffset = time;

        if (_renderStopwatch != null)
        {
            if (_renderStopwatch.IsRunning)
            {
                _renderStopwatch.Restart();
            }
            else
            {
                _renderStopwatch.Reset();
            }
        }

        CurrentTime = time;
    }

    public void ForcePause()
    {
        if (IsPlaying)
        {
            PlayPauseChartRendering();
        }
    }

    public void ForceResume()
    {
        if (!IsPlaying)
        {
            PlayPauseChartRendering();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _projectManager.PropertyChanged -= OnProjectManagerPropertyChanged;
        _messenger.UnregisterAll(this);
        StopChartRendering();
        _audioPlaybackService.Dispose();
    }

    private void RenderTimerCallback(object? sender, EventArgs e)
    {
        _renderStopwatch ??= new Stopwatch();

        double playbackSpeed = Math.Clamp(PlaybackSpeed, 0.1, 4.0);
        long scaledTicks = (long)Math.Round(_renderStopwatch.Elapsed.Ticks * playbackSpeed, MidpointRounding.AwayFromZero);
        TimeSpan currentTime = _manualTimeOffset + TimeSpan.FromTicks(scaledTicks);

        CurrentTime = currentTime;
        _audioPlaybackService.UpdatePlaybackForChartTime(currentTime);
    }

    private void OnProjectManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(_projectManager.EditingProject) && e.PropertyName != "Chart")
        {
            return;
        }

        _dispatcherTimer?.Stop();
        _renderStopwatch?.Stop();
        _renderStopwatch?.Reset();
        _audioPlaybackService.StopAndReset();

        _manualTimeOffset = TimeSpan.Zero;
        CurrentTime = TimeSpan.Zero;
        RefreshProjectBindings();
        LoadAudio(_projectManager.EditingProject?.EncodedAudio);
        OnPropertyChanged(nameof(IsPlaying));
        InvalidateRender();
    }

    private void UpdatePlaybackPosition(TimeSpan time)
    {
        _playbackPositionService.CurrentTime = time;

        var chart = CurrentChart;
        if (chart is null)
        {
            _playbackPositionService.CurrentTick = 0;
            return;
        }

        double tick = TimeTickConverter.TimeToTick(time.TotalSeconds, chart.BpmKeyFrames, chart.InitialBpm);
        _playbackPositionService.CurrentTick = Math.Max(0, (int)Math.Round(tick, MidpointRounding.AwayFromZero));
    }

    private void SnapToNearestTick()
    {
        var chart = CurrentChart;
        if (chart is null)
        {
            return;
        }

        double exactTick = TimeTickConverter.TimeToTick(_manualTimeOffset.TotalSeconds, chart.BpmKeyFrames, chart.InitialBpm);
        int snappedTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);
        double relativeTick = Math.Max(0, snappedTick);
        double seconds = TimeTickConverter.TickToTime(relativeTick, chart.BpmKeyFrames, chart.InitialBpm);

        SeekTo(TimeSpan.FromSeconds(seconds));
    }

    private void RefreshProjectBindings()
    {
        CurrentChart = _projectManager.EditingProject?.Chart;
        IllustrationBytes = _projectManager.EditingProject?.EncodedIllustration;
    }

    private void LoadAudio(string fileName)
    {
        try
        {
            _audioPlaybackService.Load(fileName);
        }
        catch (Exception ex)
        {
            UserNotificationRequested?.Invoke(this, $"Failed to load audio: {ex.Message}");
        }
    }

    private void LoadAudio(byte[]? audioBytes)
    {
        try
        {
            _audioPlaybackService.Load(audioBytes);
        }
        catch (Exception ex)
        {
            UserNotificationRequested?.Invoke(this, $"Failed to load audio: {ex.Message}");
        }
    }

    private void ApplyAudioSpeedSettings()
    {
        _audioPlaybackService.ApplySpeedSettings(PlaybackSpeed, PreserveAudioPitch);
    }

    private void InvalidateRender()
    {
        RenderInvalidated?.Invoke(this, EventArgs.Empty);
    }
}
