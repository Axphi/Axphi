using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using NAudio.Utils;
using NAudio.Wave; // 需要引用 NAudio
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Axphi.WindowsComponents.MainWindow
{
    public partial class ChartDisplay : UserControl
    {
        // 私有变量搬过来
        private MediaFoundationReader? _musicReader;
        private WasapiOut? _wasapiOut;
        private DispatcherTimer? _dispatcherTimer;
        private Stopwatch? _renderStopwatch;

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
            // 清理旧资源
            CleanUpResources();

            try
            {
                _musicReader = new MediaFoundationReader(fileName);
                _wasapiOut = new WasapiOut();
                _wasapiOut.Init(_musicReader);
            }
            catch (Exception ex)
            {
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

            // 归零
            if (_musicReader != null) _musicReader.Position = 0;
            InternalChartRenderer.Time = default;

            // ============ 【新增】强行让时间轴大管家也归零 ============
            if (this.DataContext is MainViewModel vm)
            {
                vm.Timeline.CurrentPlayTimeSeconds = 0;
            }
        }

        private void RenderTimerCallback(object? sender, EventArgs e)
        {
            // 1. 先统一获取当前的时间
            TimeSpan currentTime;
            if (_wasapiOut is not null && _wasapiOut.PlaybackState == PlaybackState.Playing)
            {
                currentTime = _wasapiOut.GetPositionTimeSpan();
            }
            else
            {
                // 没有音频时使用秒表时间
                _renderStopwatch ??= new Stopwatch();
                currentTime = _renderStopwatch.Elapsed;
            }

            // 2. 喂给上半部分的画面渲染器
            InternalChartRenderer.Time = currentTime;

            // ============ 3. 【新增】喂给下半部分 Timeline 的游标 ============
            if (this.DataContext is MainViewModel vm)
            {
                // 直接把刚才拿到的 currentTime 转换成秒，传给大管家！
                vm.Timeline.CurrentPlayTimeSeconds = currentTime.TotalSeconds;
            }
        }

        private void CleanUpResources()
        {
            StopChartRendering(); // 先停止
            _wasapiOut?.Dispose();
            _wasapiOut = null;
            _musicReader?.Dispose();
            _musicReader = null;
        }
    }
}