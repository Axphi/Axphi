using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave; // 需要引用 NAudio
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Axphi.Views
{
    public partial class ChartDisplay : UserControl
    {
        public bool ShowAuxiliaryUi
        {
            get => (bool)GetValue(ShowAuxiliaryUiProperty);
            set => SetValue(ShowAuxiliaryUiProperty, value);
        }

        public static readonly DependencyProperty ShowAuxiliaryUiProperty =
            DependencyProperty.Register(
                nameof(ShowAuxiliaryUi),
                typeof(bool),
                typeof(ChartDisplay),
                new PropertyMetadata(false));

        public bool ShowNoteCenters
        {
            get => (bool)GetValue(ShowNoteCentersProperty);
            set => SetValue(ShowNoteCentersProperty, value);
        }

        public static readonly DependencyProperty ShowNoteCentersProperty =
            DependencyProperty.Register(
                nameof(ShowNoteCenters),
                typeof(bool),
                typeof(ChartDisplay),
                new PropertyMetadata(false));

        public double BackgroundDimOpacity
        {
            get => (double)GetValue(BackgroundDimOpacityProperty);
            set => SetValue(BackgroundDimOpacityProperty, value);
        }

        public static readonly DependencyProperty BackgroundDimOpacityProperty =
            DependencyProperty.Register(
                nameof(BackgroundDimOpacity),
                typeof(double),
                typeof(ChartDisplay),
                new PropertyMetadata(0.3, OnBackgroundDimOpacityChanged));

        public double PlaybackSpeed
        {
            get => (double)GetValue(PlaybackSpeedProperty);
            set => SetValue(PlaybackSpeedProperty, value);
        }

        public static readonly DependencyProperty PlaybackSpeedProperty =
            DependencyProperty.Register(
                nameof(PlaybackSpeed),
                typeof(double),
                typeof(ChartDisplay),
                new PropertyMetadata(1.0, OnPlaybackSpeedChanged));

        public bool PreserveAudioPitch
        {
            get => (bool)GetValue(PreserveAudioPitchProperty);
            set => SetValue(PreserveAudioPitchProperty, value);
        }

        public static readonly DependencyProperty PreserveAudioPitchProperty =
            DependencyProperty.Register(
                nameof(PreserveAudioPitch),
                typeof(bool),
                typeof(ChartDisplay),
                new PropertyMetadata(true, OnPreserveAudioPitchChanged));

        private static void OnPlaybackSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ChartDisplay display)
            {
                return;
            }

            if (display.PlaybackSpeed < 0.1)
            {
                display.PlaybackSpeed = 0.1;
                return;
            }

            if (display.PlaybackSpeed > 4.0)
            {
                display.PlaybackSpeed = 4.0;
                return;
            }

            display.ApplyAudioSpeedSettings();
        }

        private static void OnPreserveAudioPitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartDisplay display)
            {
                display.ApplyAudioSpeedSettings();
            }
        }

        private static void OnBackgroundDimOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ChartDisplay display)
            {
                return;
            }

            double clamped = Math.Clamp(display.BackgroundDimOpacity, 0.0, 1.0);
            if (Math.Abs(clamped - display.BackgroundDimOpacity) > 0.000001)
            {
                display.BackgroundDimOpacity = clamped;
            }
        }

        // 私有变量搬过来
        // 把 private MediaFoundationReader? _musicReader; 替换为：
        private AudioFileReader? _musicReader;
        
        private WasapiOut? _wasapiOut;
        private DispatcherTimer? _dispatcherTimer;
        private Stopwatch? _renderStopwatch;
        private string? _temporaryAudioFilePath;
        private string? _temporaryDecodedAudioFilePath;
        private const int WasapiLatencyMilliseconds = 50;


        // 【新增】用来记住你刚刚拖拽到了哪里
        private TimeSpan _manualTimeOffset = TimeSpan.Zero;
        public ChartDisplay()
        {
            InitializeComponent();
            // 可以在 Unloaded 事件中清理资源，防止内存泄漏
            this.Unloaded += (s, e) => CleanUpResources();
        }



        // --- 供外部调用的 API ---

        /// <summary>
        /// 加载音频文件供播放器使用
        /// </summary>
        public void LoadAudio(string fileName)
        {
            ResetLoadedAudio();
            LoadAudioFromFile(fileName, isTemporaryFile: false);
        }

        public void LoadAudio(byte[]? audioBytes)
        {
            ResetLoadedAudio();

            if (audioBytes is not { Length: > 0 })
            {
                return;
            }

            string extension = GetAudioFileExtension(audioBytes);
            string temporaryAudioFilePath = Path.Combine(Path.GetTempPath(), $"axphi-{Guid.NewGuid():N}{extension}");
            File.WriteAllBytes(temporaryAudioFilePath, audioBytes);

            LoadAudioFromFile(temporaryAudioFilePath, isTemporaryFile: true);
        }

        public void LoadIllustration(byte[]? imageBytes)
        {
            InternalChartRenderer.IllustrationBytes = imageBytes;
            InternalChartRenderer.InvalidateVisual();
        }

        private void LoadAudioFromFile(string fileName, bool isTemporaryFile)
        {
            try
            {
                string playbackFilePath = PreparePlaybackFile(fileName);
                _musicReader = new AudioFileReader(playbackFilePath);
                RecreateAudioOutput();

                if (isTemporaryFile)
                {
                    _temporaryAudioFilePath = fileName;
                }
            }
            catch (Exception ex)
            {
                DeleteTemporaryDecodedAudioFile();

                if (isTemporaryFile && File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                MessageBox.Show($"Failed to load audio: {ex.Message}");
            }
        }

        // --- 内部逻辑 (从 MainWindow 搬过来的) ---

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
                _dispatcherTimer.IsEnabled ^= true; // 切换 true/false
            }

            if (_dispatcherTimer.IsEnabled)
            {
                StartAudioAtTimelineTime(_manualTimeOffset);
                _renderStopwatch.Start();
            }
            else
            {
                _wasapiOut?.Pause();
                _renderStopwatch.Stop();

                // 只要进入暂停状态，立刻触发吸附！
                SnapToNearestTick();
            }
        }

        [RelayCommand]
        private void StopChartRendering()
        {
            _dispatcherTimer?.Stop();
            _renderStopwatch?.Stop();
            _renderStopwatch?.Reset();
            _wasapiOut?.Stop();


            // 【新增】彻底归零
            _manualTimeOffset = TimeSpan.Zero;

            // 归零
            if (_musicReader != null) _musicReader.Position = 0;
            InternalChartRenderer.Time = default;
        }

        private void RenderTimerCallback(object? sender, EventArgs e)
        {
            _renderStopwatch ??= new Stopwatch();

            // 核心魔法：不论有没有音乐，统一用 "空降锚点时间 + 秒表本次跑过的时间"
            // 这样红线不仅绝对不会闪回，而且移动会极其丝滑（因为秒表精度极高）
            double playbackSpeed = Math.Clamp(PlaybackSpeed, 0.1, 4.0);
            ApplyAudioSpeedSettings();
            long scaledTicks = (long)Math.Round(_renderStopwatch.Elapsed.Ticks * playbackSpeed, MidpointRounding.AwayFromZero);
            TimeSpan currentTime = _manualTimeOffset + TimeSpan.FromTicks(scaledTicks);


            InternalChartRenderer.Time = currentTime;

            double targetAudioSeconds = currentTime.TotalSeconds;
            if (_wasapiOut != null && _musicReader != null)
            {
                _musicReader.Volume = 1.0f;

                bool isInsideAudio = targetAudioSeconds >= 0 && targetAudioSeconds < _musicReader.TotalTime.TotalSeconds;
                if (isInsideAudio)
                {
                    if (_wasapiOut.PlaybackState != PlaybackState.Playing)
                    {
                        StartAudioAtTimelineTime(currentTime);
                    }
                }
                else
                {
                    if (_wasapiOut.PlaybackState == PlaybackState.Playing)
                    {
                        _wasapiOut.Pause();
                    }
                }
            }



        }

        private void CleanUpResources()
        {
            StopChartRendering(); // 先停止
            _wasapiOut?.Dispose();
            _wasapiOut = null;
            _musicReader?.Dispose();
            _musicReader = null;
            DeleteTemporaryAudioFile();
            DeleteTemporaryDecodedAudioFile();
        }

        private void ResetLoadedAudio()
        {
            _wasapiOut?.Stop();
            _wasapiOut?.Dispose();
            _wasapiOut = null;

            _musicReader?.Dispose();
            _musicReader = null;

            DeleteTemporaryAudioFile();
            DeleteTemporaryDecodedAudioFile();
        }

        private void RecreateAudioOutput()
        {
            if (_musicReader == null)
            {
                _wasapiOut?.Dispose();
                _wasapiOut = null;
                return;
            }

            _wasapiOut?.Stop();
            _wasapiOut?.Dispose();
            _wasapiOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, WasapiLatencyMilliseconds);
            _wasapiOut.Init(_musicReader);
        }

        private void StartAudioAtTimelineTime(TimeSpan chartTime)
        {
            if (_musicReader == null)
            {
                return;
            }

            if (!TryGetTargetAudioTime(chartTime, out TimeSpan audioTime))
            {
                _wasapiOut?.Pause();
                return;
            }

            _wasapiOut?.Stop();
            _musicReader.CurrentTime = audioTime;
            ApplyAudioSpeedSettings();
            _wasapiOut?.Play();
        }

        private void ApplyAudioSpeedSettings()
        {
            // 简化路径：当前音频链路不再接入可变速 provider。
            // 保留接口，避免外部依赖被破坏。
        }

        private string PreparePlaybackFile(string sourceFilePath)
        {
            DeleteTemporaryDecodedAudioFile();

            if (string.Equals(Path.GetExtension(sourceFilePath), ".wav", StringComparison.OrdinalIgnoreCase))
            {
                return sourceFilePath;
            }

            string decodedFilePath = Path.Combine(Path.GetTempPath(), $"axphi-decoded-{Guid.NewGuid():N}.wav");
            using (var reader = new AudioFileReader(sourceFilePath))
            {
                WaveFileWriter.CreateWaveFile16(decodedFilePath, reader);
            }

            _temporaryDecodedAudioFilePath = decodedFilePath;
            return decodedFilePath;
        }

        private bool TryGetTargetAudioTime(TimeSpan chartTime, out TimeSpan audioTime)
        {
            audioTime = TimeSpan.Zero;

            if (_musicReader == null)
            {
                return false;
            }

            double targetAudioSeconds = chartTime.TotalSeconds;

            if (targetAudioSeconds < 0 || targetAudioSeconds >= _musicReader.TotalTime.TotalSeconds)
            {
                return false;
            }

            audioTime = TimeSpan.FromSeconds(targetAudioSeconds);
            return true;
        }

        private void DeleteTemporaryAudioFile()
        {
            if (string.IsNullOrWhiteSpace(_temporaryAudioFilePath))
            {
                return;
            }

            if (File.Exists(_temporaryAudioFilePath))
            {
                File.Delete(_temporaryAudioFilePath);
            }

            _temporaryAudioFilePath = null;
        }

        private void DeleteTemporaryDecodedAudioFile()
        {
            if (string.IsNullOrWhiteSpace(_temporaryDecodedAudioFilePath))
            {
                return;
            }

            if (File.Exists(_temporaryDecodedAudioFilePath))
            {
                File.Delete(_temporaryDecodedAudioFilePath);
            }

            _temporaryDecodedAudioFilePath = null;
        }

        private static string GetAudioFileExtension(byte[] audioBytes)
        {
            if (audioBytes.Length >= 4)
            {
                if (audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F')
                {
                    return ".wav";
                }

                if (audioBytes[0] == 'O' && audioBytes[1] == 'g' && audioBytes[2] == 'g' && audioBytes[3] == 'S')
                {
                    return ".ogg";
                }

                if (audioBytes[0] == 'f' && audioBytes[1] == 'L' && audioBytes[2] == 'a' && audioBytes[3] == 'C')
                {
                    return ".flac";
                }
            }

            if (audioBytes.Length >= 3 && audioBytes[0] == 'I' && audioBytes[1] == 'D' && audioBytes[2] == '3')
            {
                return ".mp3";
            }

            return ".tmp";
        }


        /// <summary>
        /// 供游标拖拽完成后，强行让音频和画面空降到指定时间
        /// </summary>
        public void SeekTo(TimeSpan time)
        {
            if (_musicReader != null)
            {
                if (TryGetTargetAudioTime(time, out TimeSpan audioTime))
                {
                    _musicReader.CurrentTime = audioTime;
                }
                else
                {
                    _musicReader.CurrentTime = TimeSpan.Zero;
                }
            }

            // 1. 记住你拖拽到的目标时间
            _manualTimeOffset = time;

            // 2. 极其关键：重置秒表！
            // 这样下次播放时，秒表会从 0 开始，加上上面的 Offset，完美衔接！
            if (_renderStopwatch != null)
            {
                if (_renderStopwatch.IsRunning)
                    _renderStopwatch.Restart(); // 边播边拖的情况
                else
                    _renderStopwatch.Reset();   // 暂停时拖的情况
            }

            InternalChartRenderer.Time = time;


        }
        

        // === 供外部调用的播放控制 API ===

        // 检查当前是否正在播放
        public bool IsPlaying => _dispatcherTimer?.IsEnabled == true;

        // 强制暂停
        public void ForcePause()
        {
            if (IsPlaying) PlayPauseChartRendering(); // 直接复用你之前写的播放/暂停逻辑
        }

        // 强制恢复播放
        public void ForceResume()
        {
            if (!IsPlaying) PlayPauseChartRendering();
        }

        // === 在 ChartDisplay 类中新增这个自动吸附方法 ===
        public void SnapToNearestTick()
        {
            if (this.DataContext is MainViewModel vm && vm.ProjectManager.EditingProject?.Chart != null)
            {
                var chart = vm.ProjectManager.EditingProject.Chart;

                double exactTick = TimeTickConverter.TimeToTick(_manualTimeOffset.TotalSeconds, chart.BpmKeyFrames, chart.InitialBpm);
                int snappedTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);
                

                // 🌟 3. 删除旧的减去 Offset 的逻辑！全局时间就是绝对时间！
                double relativeTick = snappedTick;
                if (relativeTick < 0) relativeTick = 0;

                // 4. 召唤积分器，算出这个完美整数 Tick 对应的绝对秒数！
                double seconds = TimeTickConverter.TickToTime(relativeTick, chart.BpmKeyFrames, chart.InitialBpm);

                // 5. 空降过去！
                SeekTo(TimeSpan.FromSeconds(seconds));
            }
        }


    }
}