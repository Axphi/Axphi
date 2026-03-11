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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging; // 必须加上这个


namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    
    private readonly MainViewModel _mainViewModel;


    public MainWindow(
        MainViewModel mainViewModel)
    {
        
        
        InitializeComponent();

        _mainViewModel = mainViewModel;
        DataContext = mainViewModel;


        WeakReferenceMessenger.Default.Register<AudioLoadedMessage>(this, (r, message) =>
        {
            // 收到 ViewModel 发来的消息后，让 UI 控件去加载音频
            MainChartDisplay.LoadAudio(message.FilePath);
        });

        
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(31, 31, 31);
        base.OnSourceInitialized(e);
    }

    // ====== 监听全局的鼠标滚轮事件 (支持以鼠标为轴心缩放) ======
    // ====== 监听全局的鼠标滚轮事件 (带防频闪同步黑科技) ======
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (this.DataContext is MainViewModel vm)
            {
                double mouseX = e.GetPosition(GlobalScroll).X;
                double oldScale = vm.Timeline.ZoomScale;
                double oldOffset = GlobalScroll.Value;

                double newScale = oldScale;
                if (e.Delta > 0) newScale *= 1.1;
                else if (e.Delta < 0) newScale /= 1.1;

                if (newScale < 0.01) newScale = 0.01;
                if (newScale > 100.0) newScale = 100.0;

                // 核心计算公式不变
                double ratio = newScale / oldScale;
                double newOffset = (oldOffset + mouseX) * ratio - mouseX;

                // ================= 【解决频闪的终极黑科技】 =================
                // 1. 我们不再使用 BeginInvoke 延迟了！
                // 2. 为了防止滚动条在同步赋值时把我们截断，我们先“预判”它放大后的总长度，并强行撑开它的 Maximum！
                double expectedNewMaximum = GlobalScroll.Maximum * ratio;
                GlobalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedNewMaximum);

                // 3. 现在，Maximum 足够大了，我们立刻在同一帧内，同时更新 比例 和 位置！
                // 这样刻度尺在下一次渲染时，拿到的就是完美匹配的“新比例 + 新坐标”，绝不会产生废片！
                vm.Timeline.ZoomScale = newScale;
                GlobalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

                e.Handled = true;
            }
        }
    }

    // ================= 轨道滚动同步逻辑 =================

    // 用来收集所有右侧轨道的“隐形小滚轮”
    private readonly HashSet<ScrollViewer> _trackScrollViewers = new();

    

    // 每一行轨道生成时（Loaded），把自己上报给集合；Unloaded 时移除
    private void TrackScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // HashSet 会自动去重
            _trackScrollViewers.Add(sv);
        }
    }

    private void TrackScrollViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            _trackScrollViewers.Remove(sv);
        }
    }
    

    // 当底部总滚动条被拖拽时，强制所有轨道一起滚！
    private void GlobalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 拿到当前滚动条的进度值
        double offset = e.NewValue;

        // 让所有收集到的黑轨道同步滚动
        foreach (var sv in _trackScrollViewers)
        {
            sv.ScrollToHorizontalOffset(offset);
        }
    }


    // ================= 游标拖拽逻辑 (Scrubbing) =================

    // 拖拽进行中：实时更新画面，丝滑预览
    private void Playhead_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            // 1. 算出拖拽后的新 X 坐标
            double newX = vm.Timeline.PlayheadPositionX + e.HorizontalChange;
            if (newX < 0) newX = 0;
            if (newX > vm.Timeline.TotalPixelWidth) newX = vm.Timeline.TotalPixelWidth;

            // 2. 像素 反推回 Tick
            // TotalPixelWidth 已经包含了 ZoomScale 的加成，所以直接除以总 Ticks 就是每 Tick 的实际像素宽
            double pixelsPerTick = vm.Timeline.TotalPixelWidth / (double)vm.Timeline.TotalDurationTicks;
            double currentTick = newX / pixelsPerTick;

            // 3. Tick 反推回 秒数
            var chart = vm.ProjectManager.EditingProject.Chart;
            if (chart == null) return;

            double currentBpm = 120.0;
            if (chart.BpmKeyFrames != null && chart.BpmKeyFrames.Any())
            {
                currentBpm = chart.BpmKeyFrames.First().Value;
            }

            double secondsPerTick = 1.875 / currentBpm;
            double seconds = (currentTick - chart.Offset) * secondsPerTick;
            if (seconds < 0) seconds = 0;

            // 4. 更新大管家的时间，红线会立刻跟着鼠标走！
            vm.Timeline.CurrentPlayTimeSeconds = seconds;

            // 5. 实时驱动画面渲染器（这就是高帧率丝滑预览的关键！）
            MainChartDisplay.InternalChartRenderer.Time = TimeSpan.FromSeconds(seconds);
        }
    }

    // 拖拽松手时：让音乐跟上！
    // 拖拽松手时
    private void Playhead_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            // 告诉音频播放器跳到这个时间
            MainChartDisplay.SeekTo(TimeSpan.FromSeconds(vm.Timeline.CurrentPlayTimeSeconds));
        }

        // 【新增】智能恢复：如果捏住之前在播放，松手后自动继续播放！
        if (_wasPlayingBeforeDrag)
        {
            MainChartDisplay.ForceResume();
        }
    }


    // 记住拖拽游标前，音乐是否正在播放
    private bool _wasPlayingBeforeDrag = false;
    // 刚捏住游标的一瞬间
    private void Playhead_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        // 记录按下鼠标那一刻的状态
        _wasPlayingBeforeDrag = MainChartDisplay.IsPlaying;

        // 如果正在播放，强行打断施法，进入暂停模式！
        if (_wasPlayingBeforeDrag)
        {
            MainChartDisplay.ForcePause();
        }
    }

}