using Axphi.Data;
using Axphi.Services;
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

namespace Axphi.Views
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
            // 🌟 初始化全局音效引擎，它会在后台待命
            HitSoundManager.Init();

            // ================= 🌟 优雅订阅：当设备改变时重启 BGM =================
            SystemAudioMonitor.OnDefaultDeviceChanged += ReloadBgmDevice;
            // ======================================================================

            WeakReferenceMessenger.Default.Register<ChartDisplay, UpdateRendererMessage>(this, (recipient, message) =>
            {
                // 性能优化：如果当前正在播放，那么 DispatcherTimer 每毫秒都在疯狂刷新
                // 此时就不需要我们手动触发了。只有在暂停状态下才需要强行重绘！
                if (!recipient.IsPlaying)
                {
                    // 核心魔法：命令底层的渲染器“标记为过期，准备重绘”
                    recipient.InternalChartRenderer.InvalidateVisual(); // 联系底层的 OnRender
                }
            });

            // 告诉邮局：我要监听 JudgementLinesChangedMessage
            WeakReferenceMessenger.Default.Register<ChartDisplay, JudgementLinesChangedMessage>(this, (recipient, message) =>
            {
                // 性能优化：如果当前正在播放，那么 DispatcherTimer 每毫秒都在疯狂刷新
                // 此时就不需要我们手动触发了。只有在暂停状态下才需要强行重绘！
                if (!recipient.IsPlaying)
                {
                    // 核心魔法：命令底层的渲染器“标记为过期，准备重绘”
                    recipient.InternalChartRenderer.InvalidateVisual(); // 联系底层的 OnRender
                }
            });
            // ================= 【监听刹车指令】 =================
            WeakReferenceMessenger.Default.Register<ChartDisplay, ForcePausePlaybackMessage>(this, (recipient, message) =>
            {
                // 直接调用你写好的强制暂停方法！
                // 因为你的 ForcePause 里面已经写了 if (IsPlaying) 的判断，
                // 所以就算一秒钟收到 100 封信，也绝对安全，不会来回切换！
                recipient.ForcePause();
            });
            

            // ================= 【监听强制空降指令】 =================
            WeakReferenceMessenger.Default.Register<ChartDisplay, ForceSeekMessage>(this, (recipient, message) =>
            {
                // 直接白嫖你已经写好的极其完善的 SeekTo 方法！
                // 它不仅会把渲染器的 Time 改对，还会重置秒表、把音频也切过去，完美闭环！
                recipient.SeekTo(TimeSpan.FromSeconds(message.TargetSeconds));
            });

            
        }

        // ================= 🌟 现在的复活方法变得极其简洁 =================
        private void ReloadBgmDevice()
        {
            Dispatcher.Invoke(() =>
            {
                if (_musicReader == null) return;

                bool wasPlaying = IsPlaying;

                if (_wasapiOut != null)
                {
                    if (wasPlaying) _wasapiOut.Stop();
                    _wasapiOut.Dispose();
                }

                _wasapiOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
                _wasapiOut.Init(_musicReader);

                if (wasPlaying)
                {
                    _wasapiOut.Play();
                }
            });
        }



        // --- 供外部调用的 API ---

        /// <summary>
        /// 加载音频文件供播放器使用
        /// </summary>
        public void LoadAudio(string fileName)
        {
            _wasapiOut?.Stop();
            _wasapiOut?.Dispose();
            _musicReader?.Dispose();

            try
            {
                _musicReader = new MediaFoundationReader(fileName);
                _wasapiOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
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
                // _wasapiOut?.Play();
                // 因为我们已经在 RenderTimerCallback 里做了智能判断：游标踩中图层才会出声！
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

            // ============ 强行让时间轴大管家也归零 ============
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
                var chart = vm.ProjectManager.EditingProject.Chart; // 全局共享的 chart
                // ================= 🌟 1. 提取“上一帧”的时间 =================
                // 这个值在上面还没被覆盖，所以它完美代表了之前的时间！
                // （如果你刚刚拖拽了游标，它就会等于你拖拽到的那个绝对准确的时间）
                double prevSeconds = vm.Timeline.CurrentPlayTimeSeconds;


                double currSeconds = currentTime.TotalSeconds;

                // ================= 🌟 音效判定引擎 =================
                // 只有正常正向播放时才触发音效 (如果是拖拽游标导致的时间跳跃，或者倒退，则屏蔽声音)
                if (currSeconds > prevSeconds && (currSeconds - prevSeconds) < 0.2)
                {
                    // var chart = vm.ProjectManager.EditingProject.Chart;

                    // 算出上一帧和这一帧对应的绝对 Tick
                    int prevTick = (int)Math.Round(TimeTickConverter.TimeToTick(prevSeconds, chart.BpmKeyFrames, chart.InitialBpm), MidpointRounding.AwayFromZero);
                    int currTick = (int)Math.Round(TimeTickConverter.TimeToTick(currSeconds, chart.BpmKeyFrames, chart.InitialBpm), MidpointRounding.AwayFromZero);

                    foreach (var line in chart.JudgementLines)
                    {
                        if (line.Notes == null) continue;

                        foreach (var note in line.Notes)
                        {
                            // 🌟 核心拦截：音符的 HitTime 刚好落在了这极短的两帧之间！
                            if (note.HitTime > prevTick && note.HitTime <= currTick)
                            {
                                // 取出音符类型
                                var kind = KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, currTick, note.InitialKind);
                                // 呼叫音效播放器
                                PlayHitSound(kind);
                            }
                        }
                    }


                    // ================= 🌟 新增：节拍器判定引擎 =================
                    // 只有处于播放状态，且开关开启时才判定
                    if (vm.Timeline.IsMetronomeEnabled && currSeconds > prevSeconds)
                    {
                        // 假设你的引擎里 1 拍 (Quarter Note) 是 480 Tick。
                        // ⚠️ 如果你的 Tick 分辨率是 1920 或者 120，请修改这个值！
                        double ticksPerBeat = 32.0;

                        // 计算上一帧和当前帧，分别身处全局的第几个“拍子”区间里
                        int prevBeat = (int)Math.Floor(prevTick / ticksPerBeat);
                        int currBeat = (int)Math.Floor(currTick / ticksPerBeat);

                        // 只要当前帧跨越了节拍线，且时间大于等于 0
                        if (currBeat > prevBeat && currBeat >= 0)
                        {
                            // 默认为 4/4 拍，每逢 4 的倍数就是重拍 (Downbeat)
                            bool isDownbeat = (currBeat % 4 == 0);

                            // 呼叫后台发声！（记得替换成你实际包含该方法的类名，比如 HitSoundManager）
                            HitSoundManager.PlayMetronome(isDownbeat);
                        }
                    }
                }
                // ===================================================
                // ================= 🌟 新增：智能音频启停控制器 =================
                // 算出音频图层放在了宇宙的哪个位置（Offset的物理秒数）
                double offsetSeconds = TimeTickConverter.TickToTime(chart.Offset, chart.BpmKeyFrames, chart.InitialBpm);
                // 算出当前宇宙时间减去音频位置，得到“音频文件自己该播哪一秒”
                double targetAudioSeconds = currSeconds - offsetSeconds;

                if (_wasapiOut != null && _musicReader != null)
                {
                    // 🌟 核心修复：划定严格的“音频存活区间”
                    // 必须大于 0，且必须小于音频的总时长！
                    bool isInsideAudio = targetAudioSeconds >= 0 && targetAudioSeconds < _musicReader.TotalTime.TotalSeconds;

                    if (isInsideAudio)
                    {
                        if (_wasapiOut.PlaybackState != NAudio.Wave.PlaybackState.Playing)
                        {
                            // 只要游标回到合法区间，且没在播放，就校准时间并唤醒！
                            _musicReader.CurrentTime = TimeSpan.FromSeconds(targetAudioSeconds);
                            _wasapiOut.Play();
                        }
                    }
                    else
                    {
                        // 游标还没跑到图层上，【或者已经越过了图层的尾巴】！
                        if (_wasapiOut.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                        {
                            // 强制按住它的头让它休眠，防止它在尾巴处疯狂起死回生导致崩溃！
                            _wasapiOut.Pause();
                        }
                    }
                }
                // ==========================================================

                vm.Timeline.CurrentPlayTimeSeconds = currentTime.TotalSeconds;

                // ================= 🌟 2. 加上这一句！召唤拦截探测器！ =================
                vm.Timeline.CheckWorkspaceLoop(prevSeconds, currentTime.TotalSeconds);
                // ===================================================================



            }



        }

        private void PlayHitSound(Axphi.Data.NoteKind kind)
        {
            // 直接呼叫神级引擎！
            HitSoundManager.Play(kind);
        }




        private void CleanUpResources()
        {
            StopChartRendering(); // 先停止
            _wasapiOut?.Dispose();
            _wasapiOut = null;
            _musicReader?.Dispose();
            _musicReader = null;

            // ================= 【注销逻辑】 =================
            // 控件被销毁时，告诉邮局：“别给我发信了”，释放内存！
            WeakReferenceMessenger.Default.UnregisterAll(this);


            // 🌟 退订全局事件
            SystemAudioMonitor.OnDefaultDeviceChanged -= ReloadBgmDevice;
        }


        /// <summary>
        /// 供游标拖拽完成后，强行让音频和画面空降到指定时间
        /// </summary>
        public void SeekTo(TimeSpan time)
        {
            if (this.DataContext is MainViewModel vm && vm.ProjectManager.EditingProject?.Chart != null)
            {
                var chart = vm.ProjectManager.EditingProject.Chart;

                // 🌟 算出音频图层距离宇宙起点 (Tick=0) 的物理秒数
                double offsetSeconds = TimeTickConverter.TickToTime(chart.Offset, chart.BpmKeyFrames, chart.InitialBpm);

                // 🌟 音频自己真正该播的时间 = 当前宇宙时间 - 图层开始的时间
                double audioSeconds = time.TotalSeconds - offsetSeconds;

                if (_musicReader != null)
                {
                    if (audioSeconds < 0)
                    {
                        // 如果游标在音频图层的左边，让音频回到开头待命
                        _musicReader.CurrentTime = TimeSpan.Zero;
                    }
                    else
                    {
                        // 如果游标踩在了音频图层上，让音频空降到对应进度
                        if (audioSeconds <= _musicReader.TotalTime.TotalSeconds)
                            _musicReader.CurrentTime = TimeSpan.FromSeconds(audioSeconds);
                    }
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


            // 空降完成后，立刻把最新的秒数同步给时间轴大管家！
            // 这样红线就会瞬间跳到对应的位置！
            if (this.DataContext is MainViewModel vm2)
            {
                vm2.Timeline.CurrentPlayTimeSeconds = time.TotalSeconds;
            }
            // ===================================================
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

                // 1. 拿到当前精确的小数 Tick
                double exactTick = vm.Timeline.GetExactTick();


                // 直接调用大管家统一的整数 Tick 获取方法！
                // 2. 四舍五入，吸附到最近的整数 Tick
                int snappedTick = vm.Timeline.GetCurrentTick();
                

                // 🌟 3. 删除旧的减去 Offset 的逻辑！全局时间就是绝对时间！
                double relativeTick = snappedTick;
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