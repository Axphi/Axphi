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


    // 在类成员区域添加
    private readonly DispatcherTimer _dragTimer = new DispatcherTimer();
    private Action? _currentDragAction = null; // 记录当前正在拖拽谁


    public MainWindow(
        MainViewModel mainViewModel)
    {
        
        
        InitializeComponent();

        _mainViewModel = mainViewModel;
        DataContext = mainViewModel;


        // 🌟 新增：初始化 60FPS 拖拽刷新计时器
        _dragTimer.Interval = TimeSpan.FromMilliseconds(16);
        _dragTimer.Tick += DragTimer_Tick;

        WeakReferenceMessenger.Default.Register<AudioLoadedMessage>(this, (r, message) =>
        {
            // 收到 ViewModel 发来的消息后，让 UI 控件去加载音频
            MainChartDisplay.LoadAudio(message.FilePath);
        });

        
    }


    // ================= 全局拖拽 60 帧引擎 =================

    private void DragTimer_Tick(object? sender, EventArgs e)
    {
        // 🌟 核心修复：把 TimelineMainGrid 换成了 OverlayCanvas
        Point pos = Mouse.GetPosition(OverlayCanvas);
        double edgeMargin = 30.0; // 边缘触发感应区宽度
        double speedMult = 0.5;   // 速度倍率

        double scrollDelta = 0;

        // 向右越界
        if (pos.X > OverlayCanvas.ActualWidth - edgeMargin)
        {
            scrollDelta = (pos.X - (OverlayCanvas.ActualWidth - edgeMargin)) * speedMult;
        }
        // 向左越界
        else if (pos.X < edgeMargin)
        {
            scrollDelta = (pos.X - edgeMargin) * speedMult;
        }

        if (scrollDelta != 0)
        {
            double newValue = GlobalHorizontalScroll.Value + scrollDelta;
            newValue = Math.Clamp(newValue, 0, GlobalHorizontalScroll.Maximum);
            GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newValue);
        }

        // 强制游标更新位置
        _currentDragAction?.Invoke();
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



                


                // ================= 🌟 新增：动态计算“刚好填满屏幕”的最小缩放比例 =================
                double basePixelsPerTick = 0.5; // 你的 TimelineViewModel 基础常数

                double rightPadding = 15.0; // 与 ViewModel 保持一致


                double minScale = (vm.Timeline.ViewportActualWidth- rightPadding) / (vm.Timeline.TotalDurationTicks * basePixelsPerTick);

                // 绝对不允许画面比屏幕窄！(防穿模、防走光)
                if (newScale < minScale) newScale = minScale;
                if (newScale > 100.0) newScale = 100.0;
                // ==============================================================================


                

                // 核心计算公式不变
                double ratio = newScale / oldScale;
                double newOffset = (oldOffset + mouseX) * ratio - mouseX;

                // ================= 【解决频闪的终极黑科技】 =================
                // 🌟 1. 预测新缩放下的物理总宽度
                double expectedNewTotalWidth = vm.Timeline.TotalDurationTicks * 0.5 * newScale;
                // 🌟 2. 预测正确的滚动条最大边界 (总宽 - 视口宽)
                double expectedNewMaximum = Math.Max(0, expectedNewTotalWidth - vm.Timeline.ViewportActualWidth+ rightPadding);

                GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedNewMaximum);

                // 🌟 3. 严防死守！防止缩放时，游标或画面偏移到视口之外
                if (newOffset > expectedNewMaximum) newOffset = expectedNewMaximum;
                if (newOffset < 0) newOffset = 0;

                vm.Timeline.ZoomScale = newScale;
                GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

                e.Handled = true;
            }
        }
        // 2. 🌟 新增：Shift + 滚轮 = 时间轴水平滚动 (Pan)
        else if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            // e.Delta 通常是 120 (向上滚) 或 -120 (向下滚)
            // 向上滚 -> 画面向右走 (游标向左，滚动条变小)
            // 向下滚 -> 画面向左走 (游标向右，滚动条变大)

            double scrollSensitivity = 1.0; // 滚动灵敏度，如果觉得滚得太快/太慢可以改这个系数 (比如 0.5)
            double newOffset = GlobalHorizontalScroll.Value - (e.Delta * scrollSensitivity);

            // 物理防撞墙：不能超出滚动条的极限范围
            if (newOffset < 0) newOffset = 0;
            if (newOffset > GlobalHorizontalScroll.Maximum) newOffset = GlobalHorizontalScroll.Maximum;

            // 直接给水平滚动条强行赋值，它会自动触发联动事件，带动所有轨道一起滚动！
            GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

            e.Handled = true; // 拦截掉，防止底层继续触发默认的上下垂直滚动
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


        // 🌟 新增：大滚动条一动，小地图的视野框必须跟着动！
        UpdateMinimapViewport();
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

    // 记住拖拽游标前，音乐是否正在播放
    private bool _wasPlayingBeforeDrag = false;
    // 刚捏住游标的一瞬间
    // ================= 游标拖拽逻辑 (Scrubbing) =================

    // ================= 游标拖拽逻辑 =================
    private double _playheadDragMouseOffset;

    // ================= 1. 游标 (Playhead) =================

    private void Playhead_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        // 🌟 换成 OverlayCanvas
        Point mousePos = Mouse.GetPosition(OverlayCanvas);
        if (this.DataContext is MainViewModel vm)
        {
            _playheadDragMouseOffset = vm.Timeline.PlayheadPositionX - (mousePos.X + GlobalHorizontalScroll.Value);
        }

        WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

        _currentDragAction = UpdatePlayheadPosition;
        _dragTimer.Start();
    }




    private void Playhead_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        // 鼠标在屏幕内移动时，也强制触发一次位置更新，保证丝滑
        _currentDragAction?.Invoke();
    }

    private void Playhead_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // 🌟 松手时立刻熄火！
        _dragTimer.Stop();
        _currentDragAction = null;

        if (this.DataContext is MainViewModel vm)
        {
            MainChartDisplay.SeekTo(TimeSpan.FromSeconds(vm.Timeline.CurrentPlayTimeSeconds));
        }

        if (_wasPlayingBeforeDrag)
            MainChartDisplay.ForceResume();
        else
            MainChartDisplay.SnapToNearestTick();
    }


    // 🌟 新增：游标位置的绝对计算函数
    private void UpdatePlayheadPosition()
    {
        if (this.DataContext is MainViewModel vm)
        {
            // 🌟 换成 OverlayCanvas
            Point currentPos = Mouse.GetPosition(OverlayCanvas);

            double targetAbsolutePixel = currentPos.X + GlobalHorizontalScroll.Value + _playheadDragMouseOffset;

            if (targetAbsolutePixel < 0) targetAbsolutePixel = 0;
            if (targetAbsolutePixel > vm.Timeline.TotalPixelWidth) targetAbsolutePixel = vm.Timeline.TotalPixelWidth;

            double exactTick = vm.Timeline.PixelToTick(targetAbsolutePixel);
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: true);

            double newSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(
                snappedTick, vm.Timeline.CurrentChart.BpmKeyFrames, vm.Timeline.CurrentChart.InitialBpm);

            vm.Timeline.CurrentPlayTimeSeconds = newSeconds;

            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(newSeconds));
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
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

    // ================= 【拖动鼠标 (整合了左键框选和中键平移)】 =================
    private void TimelineMainGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // ================= 🌟 1. 中键拖拽平移 (Pan) =================
        if (_isMiddlePanning && e.MiddleButton == MouseButtonState.Pressed)
        {
            Point currentMousePos = e.GetPosition(this);

            // 鼠标向左拖 (delta为负数)，代表用户的眼睛想往右看，此时滚动条的值应该变大
            double deltaX = currentMousePos.X - _middlePanStartMousePos.X;
            double newScrollValue = _middlePanStartScrollValue - deltaX;

            // 物理防撞墙
            if (newScrollValue < 0) newScrollValue = 0;
            if (newScrollValue > GlobalHorizontalScroll.Maximum) newScrollValue = GlobalHorizontalScroll.Maximum;

            // 🌟 魔法核心：直接强行修改底部大滚动条的值，大管家会自动联动所有的轨道和小地图！
            GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newScrollValue);

            e.Handled = true;
            return;
        }

        // ================= 🌟 2. 原有的左键框选逻辑 =================
        if (!_isMarqueeSelecting) return;

        // 获取当前鼠标位置 (这里用内部坐标没关系，因为画框本身就是跟随内部坐标系)
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

    // ================= 【时间标尺交互逻辑 (已接入 60 帧引擎)】 =================

    private void TimelineRuler_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 1. 捕获鼠标并强行抢夺焦点
        MainTimelineRuler.CaptureMouse();
        if (!this.IsActive) this.Activate();
        MainTimelineRuler.Focus();

        // 2. 拖拽标尺时强制暂停播放
        WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

        // 3. 将任务交给全局 60 帧引擎！
        _currentDragAction = UpdateRulerDragPosition;
        _dragTimer.Start();

        // 立刻执行一次空降
        _currentDragAction?.Invoke();
        e.Handled = true;
    }

    private void TimelineRuler_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // 鼠标在屏幕内移动时，也强制触发一次位置更新，保证丝滑
        if (e.LeftButton == MouseButtonState.Pressed && MainTimelineRuler.IsMouseCaptured)
        {
            _currentDragAction?.Invoke();
        }
    }

    private void TimelineRuler_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MainTimelineRuler.IsMouseCaptured)
        {
            MainTimelineRuler.ReleaseMouseCapture();
        }

        // 🌟 松手时立刻熄火！
        _dragTimer.Stop();
        _currentDragAction = null;

        if (this.DataContext is MainViewModel vm)
        {
            // 告诉音频播放器跳到这个时间
            MainChartDisplay.SeekTo(TimeSpan.FromSeconds(vm.Timeline.CurrentPlayTimeSeconds));

            if (_wasPlayingBeforeDrag)
                MainChartDisplay.ForceResume();
            else
                MainChartDisplay.SnapToNearestTick();
        }
    }

    // 🌟 新增：标尺拖拽的绝对计算函数
    private void UpdateRulerDragPosition()
    {
        if (this.DataContext is MainViewModel vm)
        {
            // 统一使用 OverlayCanvas 作为判定坐标系，完美匹配边界！
            Point currentPos = Mouse.GetPosition(OverlayCanvas);

            // 核心公式：点击标尺不需要“初始偏差”，鼠标指哪，游标就绝对去哪
            double targetAbsolutePixel = currentPos.X + GlobalHorizontalScroll.Value;

            // 物理撞墙限制（不准拖出整首曲子的头和尾）
            if (targetAbsolutePixel < 0) targetAbsolutePixel = 0;
            if (targetAbsolutePixel > vm.Timeline.TotalPixelWidth) targetAbsolutePixel = vm.Timeline.TotalPixelWidth;

            // 换算 Tick 并结算吸附
            double exactTick = vm.Timeline.PixelToTick(targetAbsolutePixel);
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: true);

            double newSeconds = Axphi.Utilities.TimeTickConverter.TickToTime(
                snappedTick,
                vm.Timeline.CurrentChart.BpmKeyFrames,
                vm.Timeline.CurrentChart.InitialBpm);

            vm.Timeline.CurrentPlayTimeSeconds = newSeconds;

            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(newSeconds));
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
    }

    

    // ================= 工作区拖拽逻辑 =================


    // === 左手柄 ===
    private double _workspaceLeftDragOffset;
    private double _workspaceRightDragOffset;

    // === 左手柄 ===
    private void WorkspaceLeft_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        Point mousePos = Mouse.GetPosition(OverlayCanvas);
        if (this.DataContext is MainViewModel vm)
            _workspaceLeftDragOffset = vm.Timeline.WorkspaceStartX - (mousePos.X + GlobalHorizontalScroll.Value);

        _currentDragAction = UpdateWorkspaceLeftPosition;
        _dragTimer.Start();
    }

    private void WorkspaceLeft_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _currentDragAction?.Invoke();
    }

    private void WorkspaceLeft_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) // 记得在 XAML 绑定 DragCompleted 事件
    {
        _dragTimer.Stop();
        _currentDragAction = null;
    }

    private void UpdateWorkspaceLeftPosition()
    {
        if (this.DataContext is MainViewModel vm)
        {
            Point currentPos = Mouse.GetPosition(OverlayCanvas);
            double targetAbsolutePixel = currentPos.X + GlobalHorizontalScroll.Value + _workspaceLeftDragOffset;

            // 撞墙限制（同时防止越过右手柄）
            double minDistancePixels = vm.Timeline.TickToPixel(1);
            targetAbsolutePixel = Math.Clamp(targetAbsolutePixel, 0, vm.Timeline.WorkspaceEndX - minDistancePixels);

            double exactTick = vm.Timeline.PixelToTick(targetAbsolutePixel);
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: false);

            if (snappedTick >= vm.Timeline.WorkspaceEndTick)
                snappedTick = vm.Timeline.WorkspaceEndTick - 1;

            vm.Timeline.WorkspaceStartTick = snappedTick;
        }
    }

    // === 右手柄 ===

    // ================= 工作区右手柄拖拽逻辑 =================

    // === 右手柄 ===
    private void WorkspaceRight_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        Point mousePos = Mouse.GetPosition(OverlayCanvas);
        if (this.DataContext is MainViewModel vm)
        {
            // 记录刚捏住右手柄时，鼠标和右手柄中心的绝对物理偏差
            _workspaceRightDragOffset = vm.Timeline.WorkspaceEndX - (mousePos.X + GlobalHorizontalScroll.Value);
        }

        // 将当前动作指定为更新右手柄，并启动 60fps 引擎！
        _currentDragAction = UpdateWorkspaceRightPosition;
        _dragTimer.Start();
    }

    private void WorkspaceRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        // 只要鼠标在动，就强制触发一次绝对位置计算
        _currentDragAction?.Invoke();
    }

    private void WorkspaceRight_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // 🌟 松手时立刻熄火！(记得在 XAML 里的右侧 Thumb 绑定此事件)
        _dragTimer.Stop();
        _currentDragAction = null;
    }

    // 🌟 右手柄位置的绝对计算函数 (被 DragDelta 和 全局计时器 共同调用)
    private void UpdateWorkspaceRightPosition()
    {
        if (this.DataContext is MainViewModel vm)
        {
            Point currentPos = Mouse.GetPosition(OverlayCanvas);

            // 核心公式：绝对物理坐标 = 鼠标当前X + 滚动条当前X + 初始握持偏差
            double targetAbsolutePixel = currentPos.X + GlobalHorizontalScroll.Value + _workspaceRightDragOffset;

            // 物理撞墙限制（右手柄的下限是左手柄，上限是整首曲子的总长度）
            double minDistancePixels = vm.Timeline.TickToPixel(1); // 至少保持 1 个 Tick 的距离
            targetAbsolutePixel = Math.Clamp(
                targetAbsolutePixel,
                vm.Timeline.WorkspaceStartX + minDistancePixels, // 撞左墙（防交叉）
                vm.Timeline.TotalPixelWidth                      // 撞右墙（总长度）
            );

            // 换算成精确的 Tick
            double exactTick = vm.Timeline.PixelToTick(targetAbsolutePixel);

            // 磁吸算法处理（按住 Shift 会无视磁吸）
            int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: false);

            // 逻辑兜底：防止因为吸附算法导致的 Tick 越界或重叠
            if (snappedTick <= vm.Timeline.WorkspaceStartTick)
                snappedTick = vm.Timeline.WorkspaceStartTick + 1;

            // 最终更新到 ViewModel，UI 会自动重绘
            vm.Timeline.WorkspaceEndTick = snappedTick;
        }
    }


    // ================= 全局按键拦截 (Alt, 空格等全局快捷键) =================
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // 1. 拦截 Alt 键按下，防止系统菜单抢夺焦点
        if (e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            e.Handled = true; // 拦截它！不准激活系统菜单！
        }

        // 2. 🌟 核心修复：全局拦截空格键，强制接管播放/暂停！
        if (e.Key == Key.Space)
        {
            // 防御性设计：如果焦点在输入框 (TextBox) 里，千万别拦截，让用户能正常打字！
            if (Keyboard.FocusedElement is TextBox)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            // 拦截掉这个空格键！这样它就不会触发任何按钮的 Click 事件了
            e.Handled = true;

            // 调用你之前在 ChartDisplay 里写好的公开 API 进行播放/暂停切换
            if (MainChartDisplay.IsPlaying)
            {
                MainChartDisplay.ForcePause();
            }
            else
            {
                MainChartDisplay.ForceResume();
            }
        }

        base.OnPreviewKeyDown(e);
    }

    // ================= 拦截 Alt 键抬起，彻底封死焦点丢失 =================
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            e.Handled = true;
        }
        base.OnPreviewKeyUp(e);
    }


    // ================= 【全局缩略图 (Minimap) 尺寸自适应】 =================
    private void MinimapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            // 只要窗口大小改变，或者第一次加载，就把真实的物理宽度喂给 ViewModel
            vm.Timeline.MinimapActualWidth = e.NewSize.Width;
        }
    }

    // ================= 【全局缩略图 1：自动计算当前视野】 =================
    private void UpdateMinimapViewport()
    {
        if (this.DataContext is MainViewModel vm && MainTimelineRuler != null)
        {
            // 当前屏幕左侧边缘的物理像素位置
            double leftPixel = GlobalHorizontalScroll.Value;

            // 当前屏幕的宽度 (用标尺的宽度代表可视宽度)
            double visiblePixels = MainTimelineRuler.ActualWidth;
            if (visiblePixels <= 0) return;

            // ===== 🌟 新增：将当前视口宽度同步给 ViewModel，驱动限制和游标比例更新 =====
            vm.Timeline.ViewportActualWidth = visiblePixels;


            // ================= 🌟 新增：窗口拉宽时的“防走光”自适应 =================
            double basePixelsPerTick = 0.5;
            double rightPadding = 15.0;
            
            double minScale = Math.Max(0.01, (visiblePixels - rightPadding) / (vm.Timeline.TotalDurationTicks * basePixelsPerTick));
            // 如果窗口变宽，导致当前的缩放比例不足以填满全屏，就强行把它撑满！
            if (vm.Timeline.ZoomScale < minScale)
            {
                vm.Timeline.ZoomScale = minScale;
                // 既然已经撑满全屏了，说明不需要滚动了，强行把滚动条位置归零！
                GlobalHorizontalScroll.Value = 0;
                leftPixel = 0;
            }
            // ========================================================================




            // 转换为 Tick 并更新 ViewModel
            vm.Timeline.ViewportStartTick = vm.Timeline.PixelToTick(leftPixel);
            vm.Timeline.ViewportEndTick = vm.Timeline.PixelToTick(leftPixel + visiblePixels);
        }
    }

    // 窗口或布局大小改变时，更新视野
    private void TimelineMainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMinimapViewport();
    }


    // ================= 【全局缩略图 2：拖拽蓝色滑块反向控制画面】 =================
    private void MinimapViewport_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        // 拖拽开始时可以记录一些状态，目前直接留空即可
    }

    private void MinimapViewport_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            // e.HorizontalChange 是鼠标在【小地图】里移动的微小像素量
            // 我们需要把它放大成【大时间轴】里真实的像素量！

            // 1. 算比例：小地图里的 1 像素，等于大时间轴里的多少 Tick？
            double ticksPerMinimapPixel = vm.Timeline.TotalDurationTicks / vm.Timeline.MinimapActualWidth;

            // 2. 算位移：本次拖拽代表移动了多少 Tick？
            double ticksDelta = e.HorizontalChange * ticksPerMinimapPixel;

            // 3. 换算回真实大时间轴的物理像素位移！
            double realPixelDelta = vm.Timeline.TickToPixel(ticksDelta);

            // 4. 把位移加给底部的大滚动条
            double newValue = GlobalHorizontalScroll.Value + realPixelDelta;

            // 物理防撞墙
            if (newValue < 0) newValue = 0;
            if (newValue > GlobalHorizontalScroll.Maximum) newValue = GlobalHorizontalScroll.Maximum;

            // 🌟 强行修改底部大滚动条的值，它会自动触发所有的联动！
            GlobalHorizontalScroll.Value = newValue;
        }
    }

    // ================= 【全局缩略图 2：视野框的平移与缩放】 =================

    // === 中间拖拽平移 (Pan) ===
    private void MinimapViewportPan_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) { }

    private void MinimapViewportPan_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            double ticksPerMinimapPixel = vm.Timeline.TotalDurationTicks / vm.Timeline.MinimapActualWidth;
            double realPixelDelta = vm.Timeline.TickToPixel(e.HorizontalChange * ticksPerMinimapPixel);
            double newValue = GlobalHorizontalScroll.Value + realPixelDelta;

            if (newValue < 0) newValue = 0;
            if (newValue > GlobalHorizontalScroll.Maximum) newValue = GlobalHorizontalScroll.Maximum;

            GlobalHorizontalScroll.Value = newValue; // 联动触发更新
        }
    }


    private double _minimapViewportStartTick;
    private double _minimapViewportEndTick;

    // === 左侧手柄缩放 (Zoom & Pan) ===
    private void MinimapViewportLeft_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _minimapViewportStartTick = vm.Timeline.ViewportStartTick;
            _minimapViewportEndTick = vm.Timeline.ViewportEndTick;
        }
    }

    private void MinimapViewportLeft_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm && MainTimelineRuler.ActualWidth > 0)
        {
            double ticksPerPixel = vm.Timeline.TotalDurationTicks / vm.Timeline.MinimapActualWidth;
            _minimapViewportStartTick += e.HorizontalChange * ticksPerPixel;

            // 限制：视野最少保持 100 Tick，防止无限放大导致崩溃
            double minVisibleTicks = 100;
            if (_minimapViewportStartTick > _minimapViewportEndTick - minVisibleTicks)
                _minimapViewportStartTick = _minimapViewportEndTick - minVisibleTicks;
            if (_minimapViewportStartTick < 0)
                _minimapViewportStartTick = 0;

            ApplyViewportChange(vm, _minimapViewportStartTick, _minimapViewportEndTick);
        }
    }

    // === 右侧手柄缩放 (Zoom & Pan) ===
    private void MinimapViewportRight_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _minimapViewportStartTick = vm.Timeline.ViewportStartTick;
            _minimapViewportEndTick = vm.Timeline.ViewportEndTick;
        }
    }

    private void MinimapViewportRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm && MainTimelineRuler.ActualWidth > 0)
        {
            double ticksPerPixel = vm.Timeline.TotalDurationTicks / vm.Timeline.MinimapActualWidth;
            _minimapViewportEndTick += e.HorizontalChange * ticksPerPixel;

            double minVisibleTicks = 100;
            if (_minimapViewportEndTick < _minimapViewportStartTick + minVisibleTicks)
                _minimapViewportEndTick = _minimapViewportStartTick + minVisibleTicks;
            if (_minimapViewportEndTick > vm.Timeline.TotalDurationTicks)
                _minimapViewportEndTick = vm.Timeline.TotalDurationTicks;

            ApplyViewportChange(vm, _minimapViewportStartTick, _minimapViewportEndTick);
        }
    }

    // === 核心：将视野的变化转换为底层 ZoomScale 和 滚动条的改变！ ===
    private void ApplyViewportChange(MainViewModel vm, double startTick, double endTick)
    {
        double visibleTicks = endTick - startTick;
        double rulerWidth = MainTimelineRuler.ActualWidth;
        double basePixelsPerTick = 0.5; // 这是你定义在 TimelineViewModel 里的基础常数

        // 1. 根据新的可视 Tick 数量，反推算出需要的 ZoomScale
        double newZoom = rulerWidth / (visibleTicks * basePixelsPerTick);



        // ================= 🌟 新增：同样在这里限制最小缩放比例 =================
        double rightPadding = 15.0;
        double minScale = (vm.Timeline.ViewportActualWidth - rightPadding) / (vm.Timeline.TotalDurationTicks * basePixelsPerTick);

        if (newZoom < minScale) newZoom = minScale;
        if (newZoom > 100.0) newZoom = 100.0;
        // ========================================================================

        // ================= 🌟 核心修复 =================
        double expectedNewTotalWidth = vm.Timeline.TotalDurationTicks * basePixelsPerTick * newZoom;
        double expectedNewMaximum = Math.Max(0, expectedNewTotalWidth - vm.Timeline.ViewportActualWidth+ rightPadding);

        GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedNewMaximum);

        // 3. 应用新的缩放比例和滚动条位置
        vm.Timeline.ZoomScale = newZoom;
        double newOffset = startTick * basePixelsPerTick * newZoom;

        // 严防死守
        if (newOffset > expectedNewMaximum) newOffset = expectedNewMaximum;
        if (newOffset < 0) newOffset = 0;

        GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

        // 强制刷新一次视野！
        UpdateMinimapViewport();
    }


    // ================= 中键拖拽平移状态 =================
    private bool _isMiddlePanning = false;
    private Point _middlePanStartMousePos;
    private double _middlePanStartScrollValue;

    // ================= 【中键拖拽逻辑 1：按下中键】 =================
    private void TimelineMainGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isMiddlePanning = true;

            // 🌟 极其重要的防抖细节：取相对于 Window 纯物理屏幕的绝对坐标！
            // 如果取 Canvas 内部的相对坐标，拖拽时画面移动会导致坐标自身发生突变，画面会疯狂鬼畜！
            _middlePanStartMousePos = e.GetPosition(this);
            _middlePanStartScrollValue = GlobalHorizontalScroll.Value;

            TimelineMainGrid.CaptureMouse();

            // 🌟 新增：将鼠标变成左右拖拽的箭头图标！
            // 如果你喜欢四向箭头，可以改成 Cursors.SizeAll
            // 如果你喜欢小手，可以改成 Cursors.Hand
            TimelineMainGrid.Cursor = Cursors.SizeWE;


            e.Handled = true;
        }
    }

    // ================= 【中键拖拽逻辑 2：松开中键】 =================
    private void TimelineMainGrid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Released && _isMiddlePanning)
        {
            _isMiddlePanning = false;
            TimelineMainGrid.ReleaseMouseCapture();

            // 🌟 新增：恢复默认鼠标指针！
            TimelineMainGrid.Cursor = null;

            e.Handled = true;
        }
    }

    

}