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
using System.Windows.Controls.Primitives;


namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    
    private readonly MainViewModel _mainViewModel;

    // 记录框选起点的坐标
    private Point _marqueeStartPoint;
    private bool _isMarqueeSelecting = false;
    // 在类的最上面，声明一个变量来记住画框起手时的按键状态
    private ModifierKeys _marqueeModifiers = ModifierKeys.None;



    // 声明两个全局变量
    private Point _playheadLastMousePos;
    private double _playheadVirtualPixelX;

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
                // 这样刻度尺在下一次渲染时，拿到的就是完美匹配的“新比例 + 新坐标”，绝不会產生废片！
                vm.Timeline.ZoomScale = newScale;
                GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

                e.Handled = true;
            }
        }
    }

    

    // 当底部总滚动条被拖拽时，强制所有轨道一起滚！
    private void GlobalHorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 拿到当前滚动条的进度值
        double offset = e.NewValue;

        // 🌟 核心修改：大喇叭广播！所有活着的 TrackControl 收到消息后会自动滚动！
        WeakReferenceMessenger.Default.Send(new SyncHorizontalScrollMessage(offset));

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
        Point currentPos = Mouse.GetPosition(TimelineMainGrid);
        double stableDeltaX = currentPos.X - _playheadLastMousePos.X; // 自己算稳定位移！
        _playheadLastMousePos = currentPos;

        if (this.DataContext is MainViewModel vm)
        {
            _playheadVirtualPixelX += stableDeltaX;
            if (_playheadVirtualPixelX < 0) _playheadVirtualPixelX = 0;

            double exactTick = vm.Timeline.PixelToTick(_playheadVirtualPixelX);
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: true);

            double newSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(
                snappedTick,
                vm.Timeline.CurrentChart.BpmKeyFrames,
                vm.Timeline.CurrentChart.InitialBpm);

            vm.Timeline.CurrentPlayTimeSeconds = newSeconds;

            // ================= 🌟 补上这两句发消息！ =================
            // 1. 告诉播放引擎强制跳转到这个时间（防止拖拽时和播放器打架）
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ForceSeekMessage(newSeconds));

            // 2. 告诉右侧的渲染器立刻重绘画面！
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
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
        // TimelineMainGrid 也是绝对静止的！
        _playheadLastMousePos = Mouse.GetPosition(TimelineMainGrid);
        if (this.DataContext is MainViewModel vm)
        {
            _playheadVirtualPixelX = vm.Timeline.PlayheadPositionX;
        }

        WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
    }




    // ================= 【框选逻辑 1：按下鼠标】 =================
    private void TimelineMainGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 🌟 核心修复：防误触终极版！顺藤摸瓜找控件
        DependencyObject? current = e.OriginalSource as DependencyObject;
        while (current != null && current != TimelineMainGrid)
        {
            string typeName = current.GetType().Name;

            // 如果点到了以下任何交互控件，立刻撤退！把事件完整还给它们！
            if (current is System.Windows.Controls.Primitives.ButtonBase || // 涵盖普通的 Button 和 ToggleButton (下拉箭头)
                current is System.Windows.Controls.Primitives.ScrollBar ||  // 涵盖滚动条
                current is System.Windows.Controls.Primitives.Thumb ||      // 涵盖关键帧小菱形、时间轴红色游标
                current is TextBox ||                                       // 涵盖输入框
                typeName.Contains("DraggableValueBox") ||                   // 涵盖你自定义的数值拖拽框
                typeName.Contains("DraggableOptionBox") ||                  // 涵盖你自定义的选项拖拽框
                typeName.Contains("TimelineRuler") ||                       // 新增这一行！给标尺颁发免死金牌！
                (current is StackPanel sp && sp.Name == "JudgementLineLayer"))
            {
                return; // 直接返回，千万别设 e.Handled = true 
            }

            // 没找到就继续往上一级父元素找
            current = VisualTreeHelper.GetParent(current);
        }

        // 记录当前的按键状态，留给等会儿松手时结算用！
        _marqueeModifiers = Keyboard.Modifiers;

        // 确定点在空白处了，正式开始画框
        _isMarqueeSelecting = true;
        _marqueeStartPoint = e.GetPosition(OverlayCanvas);

        Canvas.SetLeft(MarqueeRect, _marqueeStartPoint.X);
        Canvas.SetTop(MarqueeRect, _marqueeStartPoint.Y);
        MarqueeRect.Width = 0;
        MarqueeRect.Height = 0;
        MarqueeRect.Visibility = Visibility.Visible;

        TimelineMainGrid.CaptureMouse();
        e.Handled = true; // 拦截事件，专心画框
    }

    // ================= 【框选逻辑 2：拖动鼠标】 =================
    private void TimelineMainGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMarqueeSelecting) return;

        // 获取当前鼠标位置
        Point currentPoint = e.GetPosition(OverlayCanvas);

        // 永远取起点和当前点之间最小的作为左上角坐标（完美支持向四个方向拖拽）
        double x = Math.Min(_marqueeStartPoint.X, currentPoint.X);
        double y = Math.Min(_marqueeStartPoint.Y, currentPoint.Y);
        double width = Math.Abs(_marqueeStartPoint.X - currentPoint.X);
        double height = Math.Abs(_marqueeStartPoint.Y - currentPoint.Y);

        // 实时更新框的位置和大小
        Canvas.SetLeft(MarqueeRect, x);
        Canvas.SetTop(MarqueeRect, y);
        MarqueeRect.Width = width;
        MarqueeRect.Height = height;
    }

    // ================= 【框选逻辑 3：松开鼠标并结算】 =================
    private void TimelineMainGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMarqueeSelecting) return;

        _isMarqueeSelecting = false;
        TimelineMainGrid.ReleaseMouseCapture();
        MarqueeRect.Visibility = Visibility.Collapsed;

        // 🌟 超级细节：如果只是在空白处单纯“点”了一下（宽和高都是 0），且没按修饰键
        if (MarqueeRect.Width == 0 || MarqueeRect.Height == 0)
        {
            if (_marqueeModifiers == ModifierKeys.None)
            {
                // 发大喇叭：让全场所有关键帧都暗下去！(传 null 代表没人有免死金牌)
                WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", null));

                // 2. 🌟 新增：清空所有音符（取消选中音符本体）
                WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", null));
            }
            return;
        }

        // ================= 终极模式结算 =================

        // 1. 如果什么修饰键都没按（排他框选）：先发广播，清空全场所有选中状态！
        if (_marqueeModifiers == ModifierKeys.None)
        {
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", null));
            // 2. 🌟 新增：清空所有音符（取消选中音符本体）
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", null));
        }

        GeneralTransform marqueeTransform = MarqueeRect.TransformToAncestor(TimelineMainGrid);
        Rect marqueeBounds = marqueeTransform.TransformBounds(new Rect(0, 0, MarqueeRect.Width, MarqueeRect.Height));

        var allThumbs = FindVisualChildren<Thumb>(TimelineMainGrid);

        foreach (var thumb in allThumbs)
        {
            if (thumb.DataContext != null && 
                thumb.DataContext.GetType().Name.Contains("KeyFrameUIWrapper") ||
                thumb.DataContext is Axphi.ViewModels.NoteViewModel)
            {
                try
                {
                    GeneralTransform transform = thumb.TransformToAncestor(TimelineMainGrid);
                    Rect thumbBounds = transform.TransformBounds(new Rect(0, 0, thumb.ActualWidth, thumb.ActualHeight));

                    // 2. 灵魂相交判定！
                    if (marqueeBounds.IntersectsWith(thumbBounds))
                    {
                        dynamic wrapper = thumb.DataContext;

                        // 3. 根据起手时的修饰键，执行不同的命运
                        if (_marqueeModifiers.HasFlag(ModifierKeys.Control))
                        {
                            // Ctrl 框选：取反 (Toggle)
                            wrapper.IsSelected = !wrapper.IsSelected;
                        }
                        else if (_marqueeModifiers.HasFlag(ModifierKeys.Shift))
                        {
                            // Shift 框选：纯加选 (Add)
                            wrapper.IsSelected = true;
                        }
                        else
                        {
                            // 普通框选 (None)：因为前面已经清空了全场，这里直接点亮即可 (排他)
                            wrapper.IsSelected = true;
                        }
                    }
                }
                catch
                {
                    // 防止虚拟化控件报错
                }
            }
        }
    }

    // ================= 【工具：递归查找视觉子元素】 =================
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);

            if (child is T t)
                yield return t;

            foreach (T childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }


    // ================= 【时间标尺交互逻辑】 =================

    private void TimelineRuler_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 捕获鼠标，这样即使拖到了标尺外面，也能继续响应移动事件
        MainTimelineRuler.CaptureMouse();
        SeekFromRulerMousePosition(e.GetPosition(MainTimelineRuler));
        e.Handled = true;
    }

    private void TimelineRuler_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // 如果左键被按下且被捕获，说明用户正在标尺上拖拽（Scrubbing）
        if (e.LeftButton == MouseButtonState.Pressed && MainTimelineRuler.IsMouseCaptured)
        {
            SeekFromRulerMousePosition(e.GetPosition(MainTimelineRuler));
        }
    }

    private void TimelineRuler_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MainTimelineRuler.IsMouseCaptured)
        {
            MainTimelineRuler.ReleaseMouseCapture();
        }
    }

    // 核心换算与发信方法
    private void SeekFromRulerMousePosition(Point mousePos)
    {
        // 1. 拿到鼠标在总画布中的绝对物理像素位置 (当前点击的屏幕 X + 滚动条滚过的 X)
        double absolutePixelX = mousePos.X + MainTimelineRuler.VisibleOffsetX;

        // 防止用户拖拽到 0 以前导致报错
        if (absolutePixelX < 0) absolutePixelX = 0;

        if (this.DataContext is Axphi.ViewModels.MainViewModel vm)
        {
            // 2. 将像素完美转换回精确的小数 Tick
            double exactTick = vm.Timeline.PixelToTick(absolutePixelX);

            // 四舍五入，吸附到最近的整数 Tick
            int snappedTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);

            var chart = vm.ProjectManager.EditingProject?.Chart;
            if (chart != null)
            {
                // 3. 减去 Offset，获取真正用于音频计算的 Tick
                double relativeTick = snappedTick - chart.Offset;

                // 4. 将 Tick 换算成物理现实的秒数
                // (调用你封装好的 TimeTickConverter)
                double seconds = Axphi.Utilities.TimeTickConverter.TickToTime(relativeTick, chart.BpmKeyFrames, chart.InitialBpm);

                // 音频不能在负数时间播放，所以限制最低为 0
                if (seconds < 0) seconds = 0;

                // 5. 大喊一声：全军空降！
                // ChartDisplay 听到后会自动切音频、切画面，并同步把红线拉过来！
                WeakReferenceMessenger.Default.Send(new Axphi.ViewModels.ForceSeekMessage(seconds));
            }
        }
    }

    // ================= 工作区拖拽逻辑 =================
    private Point _workspaceLastMousePos;
    private double _workspaceVirtualPixelX;

    // === 左手柄 ===
    private void WorkspaceLeft_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _workspaceLastMousePos = Mouse.GetPosition(TimelineMainGrid);
        if (this.DataContext is MainViewModel vm)
            _workspaceVirtualPixelX = vm.Timeline.WorkspaceStartX;
    }

    private void WorkspaceLeft_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        Point currentPos = Mouse.GetPosition(TimelineMainGrid);
        double stableDeltaX = currentPos.X - _workspaceLastMousePos.X;
        _workspaceLastMousePos = currentPos;

        if (this.DataContext is MainViewModel vm)
        {
            _workspaceVirtualPixelX += stableDeltaX;
            if (_workspaceVirtualPixelX < 0) _workspaceVirtualPixelX = 0;

            // 物理防撞墙：左手柄不能越过右手柄，最少保持 10 像素距离
            if (_workspaceVirtualPixelX > vm.Timeline.WorkspaceEndX - 10)
                _workspaceVirtualPixelX = vm.Timeline.WorkspaceEndX - 10;

            double exactTick = vm.Timeline.PixelToTick(_workspaceVirtualPixelX);
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: false);

            vm.Timeline.WorkspaceStartTick = snappedTick;
        }
    }

    // === 右手柄 ===
    private void WorkspaceRight_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _workspaceLastMousePos = Mouse.GetPosition(TimelineMainGrid);
        if (this.DataContext is MainViewModel vm)
            _workspaceVirtualPixelX = vm.Timeline.WorkspaceEndX;
    }

    private void WorkspaceRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        Point currentPos = Mouse.GetPosition(TimelineMainGrid);
        double stableDeltaX = currentPos.X - _workspaceLastMousePos.X;
        _workspaceLastMousePos = currentPos;

        if (this.DataContext is MainViewModel vm)
        {
            _workspaceVirtualPixelX += stableDeltaX;

            // 物理防撞墙：右手柄不能越过左手柄
            if (_workspaceVirtualPixelX < vm.Timeline.WorkspaceStartX + 10)
                _workspaceVirtualPixelX = vm.Timeline.WorkspaceStartX + 10;

            double exactTick = vm.Timeline.PixelToTick(_workspaceVirtualPixelX);
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: false);

            vm.Timeline.WorkspaceEndTick = snappedTick;
        }
    }
}