using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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


        // 【新增】用来记住你刚刚拖拽到了哪里
        private TimeSpan _manualTimeOffset = TimeSpan.Zero;
        public ChartDisplay()
        {
            InitializeComponent();
            // 可以在 Unloaded 事件中清理资源，防止内存泄漏
            this.Unloaded += (s, e) => CleanUpResources();

            // ================= 【新增消息订阅】 =================
            // 告诉邮局：我要监听 JudgementLinesChangedMessage
            WeakReferenceMessenger.Default.Register<ChartDisplay, JudgementLinesChangedMessage>(this, (recipient, message) =>
            {
                // 性能优化：如果当前正在播放，那么 DispatcherTimer 每毫秒都在疯狂刷新
                // 此时就不需要我们手动触发了。只有在暂停状态下才需要强行重绘！
                if (!recipient.IsPlaying)
                {
                    // 核心魔法：命令底层的渲染器“标记为过期，准备重绘”
                    recipient.InternalChartRenderer.InvalidateVisual();
                }
            });
            // ===================================================
            // ================= 【新增：监听刹车指令】 =================
            WeakReferenceMessenger.Default.Register<ChartDisplay, ForcePausePlaybackMessage>(this, (recipient, message) =>
            {
                // 直接调用你写好的强制暂停方法！
                // 因为你的 ForcePause 里面已经写了 if (IsPlaying) 的判断，
                // 所以就算一秒钟收到 100 封信，也绝对安全，不会来回切换！
                recipient.ForcePause();
            });
            

            // ================= 【新增：监听强制空降指令】 =================
            WeakReferenceMessenger.Default.Register<ChartDisplay, ForceSeekMessage>(this, (recipient, message) =>
            {
                // 直接白嫖你已经写好的极其完善的 SeekTo 方法！
                // 它不仅会把渲染器的 Time 改对，还会重置秒表、把音频也切过去，完美闭环！
                recipient.SeekTo(TimeSpan.FromSeconds(message.TargetSeconds));
            });
            
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

            // ============ 【新增】强行让时间轴大管家也归零 ============
            if (this.DataContext is MainViewModel vm)
            {
                vm.Timeline.CurrentPlayTimeSeconds = 0;
            }
        }

        private void RenderTimerCallback(object? sender, EventArgs e)
        {
            _renderStopwatch ??= new Stopwatch();

            // 核心魔法：不论有没有音乐，统一用 "空降锚点时间 + 秒表本次跑过的时间"
            // 这样红线不仅绝对不会闪回，而且移动会极其丝滑（因为秒表精度极高）
            TimeSpan currentTime = _manualTimeOffset + _renderStopwatch.Elapsed;

            InternalChartRenderer.Time = currentTime;

            if (this.DataContext is MainViewModel vm)
            {
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

            // ================= 【新增注销逻辑】 =================
            // 控件被销毁时，告诉邮局：“别给我发信了”，释放内存！
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }


        /// <summary>
        /// 供游标拖拽完成后，强行让音频和画面空降到指定时间
        /// </summary>
        public void SeekTo(TimeSpan time)
        {
            if (_musicReader != null)
            {
                _musicReader.CurrentTime = time;
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

        // === 【新增】供外部调用的播放控制 API ===

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

                // 1. 拿到当前精确的小数 Tick
                double exactTick = vm.Timeline.GetExactTick();


                // 直接调用大管家统一的整数 Tick 获取方法！
                // 2. 四舍五入，吸附到最近的整数 Tick
                int snappedTick = vm.Timeline.GetCurrentTick();
                
                

                // 3. 减去 Offset，准备反推时间
                double relativeTick = snappedTick - chart.Offset;
                if (relativeTick < 0) relativeTick = 0;

                // 4. 召唤积分器，算出这个完美整数 Tick 对应的绝对秒数！
                double seconds = TimeTickConverter.TickToTime(relativeTick, chart.BpmKeyFrames, chart.InitialBpm);

                // 5. 空降过去！
                SeekTo(TimeSpan.FromSeconds(seconds));
                vm.Timeline.CurrentPlayTimeSeconds = seconds;
            }
        }


    }
}