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
                double mouseX = e.GetPosition(GlobalHorizontalScroll).X;
                double oldScale = vm.Timeline.ZoomScale;
                double oldOffset = GlobalHorizontalScroll.Value;

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
                double expectedNewMaximum = GlobalHorizontalScroll.Maximum * ratio;
                GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedNewMaximum);

                // 3. 现在，Maximum 足够大了，我们立刻在同一帧内，同时更新 比例 和 位置！
                // 这样刻度尺在下一次渲染时，拿到的就是完美匹配的“新比例 + 新坐标”，绝不会产生废片！
                vm.Timeline.ZoomScale = newScale;
                GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

                e.Handled = true;
            }
        }
    }

    // ================= 轨道滚动同步逻辑 =================

    // 用来收集所有右侧轨道的“隐形小滚轮”
    private readonly HashSet<ScrollViewer> _horizontaltrackScrollViewers = new();
    


    // 每一行轨道生成时（Loaded），把自己上报给集合；Unloaded 时移除
    private void HorizontalTrackScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // HashSet 会自动去重
            _horizontaltrackScrollViewers.Add(sv);
        }
    }

    private void HorizontalTrackScrollViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            _horizontaltrackScrollViewers.Remove(sv);
        }
    }


    

    // 当底部总滚动条被拖拽时，强制所有轨道一起滚！
    private void GlobalHorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 拿到当前滚动条的进度值
        double offset = e.NewValue;

        // 让所有收集到的黑轨道同步滚动
        foreach (var sv in _horizontaltrackScrollViewers)
        {
            sv.ScrollToHorizontalOffset(offset);
        }

        // ==========================================
        // 【修改位置：新增这一段】
        // 滚动条往右滚了 offset 距离，我们就把游标往左视觉平移 -offset 距离
        // 这样它就能精准出现在屏幕里了！
        // ==========================================
        if (PlayheadTransform != null)
        {
            PlayheadTransform.X = -offset;
        }
    }


    private readonly HashSet<ScrollViewer> _verticalTrackScrollViewers = new();
    // 注册/注销 Vertical ScrollViewer 的 Loaded/Unloaded 处理器
    private void VerticalTrackScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // 加入集合（HashSet 会去重）
            _verticalTrackScrollViewers.Add(sv);

            // 订阅 ScrollChanged 以便在用户滚动（鼠标/触摸）时更新全局 scrollbar 的范围与值
            sv.ScrollChanged += VerticalTrackScrollViewer_ScrollChanged;

            // 初始化全局 scrollbar 的范围/视口（防止刚显示时值不正确）
            GlobalVerticalScroll.Minimum = 0;
            GlobalVerticalScroll.Maximum = sv.ScrollableHeight;
            GlobalVerticalScroll.ViewportSize = sv.ViewportHeight;
            GlobalVerticalScroll.SmallChange = 16;
            GlobalVerticalScroll.LargeChange = sv.ViewportHeight;
        }
    }


    private void VerticalTrackScrollViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            _verticalTrackScrollViewers.Remove(sv);
            sv.ScrollChanged -= VerticalTrackScrollViewer_ScrollChanged;
        }
    }

    private void VerticalTrackScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        // 更新全局 scrollbar 的元信息（maximum / viewport）
        GlobalVerticalScroll.Maximum = sv.ScrollableHeight;
        GlobalVerticalScroll.ViewportSize = sv.ViewportHeight;

        // 如果这是唯一或主控 ScrollViewer，则把值同步到全局 scrollbar
        // 直接设置 Value 会触发 GlobalVerticalScrollBar_ValueChanged，但那只会把值推回各个 sv（幂等）
        GlobalVerticalScroll.Value = sv.VerticalOffset;
    }

    // 全局 Vertical Scroll 的 ValueChanged：把值推进所有注册的 ScrollViewer
    private void GlobalVerticalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        double offset = e.NewValue;

        foreach (var sv in _verticalTrackScrollViewers)
        {
            // 保护：确保 offset 在合法范围内
            double safeOffset = Math.Max(0, Math.Min(offset, sv.ScrollableHeight));
            sv.ScrollToVerticalOffset(safeOffset);
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

            // 先减去谱面偏移，得到纯粹的相对 Tick
            double relativeTick = currentTick - chart.Offset;
            if (relativeTick < 0) relativeTick = 0;

            // 🌟 召唤你写好的绝对映射神器！它会自动处理跨越 BPM 关键帧时的折线计算！
            double seconds = TimeTickConverter.TickToTime(relativeTick, chart.BpmKeyFrames, 120.0);

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
        else
        {
            // 🌟 核心修复：如果在暂停状态下拖拽游标，松手的一瞬间也要吸附！
            MainChartDisplay.SnapToNearestTick();
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

    private void InnerTrack_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 保护机制：如果你按住了 Alt (触发你的全局缩放)，或者 Shift，说明你有其他意图，我们不干预
        if (Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Shift)
        {
            return;
        }

        // 1. 强行截断！不准内部 ScrollViewer 吞噬这个纯上下滚动事件
        e.Handled = true;

        // 2. 伪造/克隆一个一模一样的新滚轮事件
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };

        // 3. 获取当前控件的父级，并把新事件强行“甩”给父级，让它继续向上冒泡，
        // 直到被最外层的那个垂直 ScrollViewer 捕获到！
        if (sender is UIElement element)
        {
            var parent = VisualTreeHelper.GetParent(element) as UIElement;
            parent?.RaiseEvent(eventArg);
        }
    }

}