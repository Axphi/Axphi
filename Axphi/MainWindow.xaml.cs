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
using Axphi.Views;


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
    private bool _marqueePreserveNoteSelection = false;
    private DependencyObject? _marqueeSelectionScope;


    // 拖拽状态机已下沉到协调器：主窗口只负责事件路由
    private readonly TimelineContinuousDragCoordinator _dragCoordinator;
    private readonly TimelineMarqueeSelectionService _marqueeSelectionService = new();
    private readonly TimelineInteractionController _timelineInteractionController = new();
    private double _lastTimelineLeftPanelWidth = -1;


    public MainWindow(
        MainViewModel mainViewModel)
    {
        
        
        InitializeComponent();

        _mainViewModel = mainViewModel;
        DataContext = mainViewModel;


        _dragCoordinator = new TimelineContinuousDragCoordinator(
            getPointerXOnSurface: () => Mouse.GetPosition(OverlayCanvas).X,
            getSurfaceWidth: () => OverlayCanvas.ActualWidth,
            getScrollValue: () => GlobalHorizontalScroll.Value,
            setScrollValue: value => GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, value),
            getScrollMaximum: () => GlobalHorizontalScroll.Maximum);

        WeakReferenceMessenger.Default.Register<AudioLoadedMessage>(this, (r, message) =>
        {
            // 收到 ViewModel 发来的消息后，让 UI 控件去加载音频
            MainChartDisplay.LoadAudio(message.FilePath);
        });

        WeakReferenceMessenger.Default.Register<IllustrationLoadedMessage>(this, (r, message) =>
        {
            MainChartDisplay.LoadIllustration(_mainViewModel.ProjectManager.EditingProject.EncodedIllustration);
        });

        WeakReferenceMessenger.Default.Register<ProjectLoadedMessage>(this, (r, message) =>
        {
            ApplyDisplaySettingsFromMetadata();
            MainChartDisplay.LoadAudio(_mainViewModel.ProjectManager.EditingProject.EncodedAudio);
            MainChartDisplay.LoadIllustration(_mainViewModel.ProjectManager.EditingProject.EncodedIllustration);
            Dispatcher.BeginInvoke(new Action(RestoreHorizontalScrollFromTimeline), DispatcherPriority.Loaded);
        });

        Loaded += (_, _) =>
        {
            SyncTimelinePanelWidths();
            RestoreHorizontalScrollFromTimeline();
        };

        RegisterDisplayMetadataSync();
        ApplyDisplaySettingsFromMetadata();

        
    }

    private void RegisterDisplayMetadataSync()
    {
        DependencyPropertyDescriptor
            .FromProperty(ChartDisplay.PlaybackSpeedProperty, typeof(ChartDisplay))
            ?.AddValueChanged(MainChartDisplay, (_, _) => PersistDisplaySettingsToMetadata());

        DependencyPropertyDescriptor
            .FromProperty(ChartDisplay.BackgroundDimOpacityProperty, typeof(ChartDisplay))
            ?.AddValueChanged(MainChartDisplay, (_, _) => PersistDisplaySettingsToMetadata());

        DependencyPropertyDescriptor
            .FromProperty(ChartDisplay.PreserveAudioPitchProperty, typeof(ChartDisplay))
            ?.AddValueChanged(MainChartDisplay, (_, _) => PersistDisplaySettingsToMetadata());
    }

    private void ApplyDisplaySettingsFromMetadata()
    {
        var metadata = GetOrCreateProjectMetadata();
        MainChartDisplay.PlaybackSpeed = metadata.PlaybackSpeed;
        MainChartDisplay.BackgroundDimOpacity = metadata.BackgroundDimOpacity;
        MainChartDisplay.PreserveAudioPitch = metadata.PreserveAudioPitch;
    }

    private void PersistDisplaySettingsToMetadata()
    {
        var metadata = GetOrCreateProjectMetadata();
        metadata.PlaybackSpeed = MainChartDisplay.PlaybackSpeed;
        metadata.BackgroundDimOpacity = MainChartDisplay.BackgroundDimOpacity;
        metadata.PreserveAudioPitch = MainChartDisplay.PreserveAudioPitch;
    }

    private ProjectMetadata GetOrCreateProjectMetadata()
    {
        _mainViewModel.ProjectManager.EditingProject ??= new Project();
        _mainViewModel.ProjectManager.EditingProject.Metadata ??= new ProjectMetadata();
        return _mainViewModel.ProjectManager.EditingProject.Metadata;
    }

    // ================= 全局拖拽 60 帧引擎 =================

    private void RestoreHorizontalScrollFromTimeline()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        double expectedMaximum = Math.Max(0, vm.Timeline.MaxScrollOffset);
        GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedMaximum);

        double restoredOffset = Math.Clamp(vm.Timeline.CurrentHorizontalScrollOffset, 0, expectedMaximum);
        GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, restoredOffset);
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

                double zoomStep = vm.Timeline.ZoomStepFactor;
                double newScale = oldScale;
                if (e.Delta > 0) newScale *= zoomStep;
                else if (e.Delta < 0) newScale /= zoomStep;



                


                // ================= 🌟 新增：动态计算“刚好填满屏幕”的最小缩放比例 =================
                double basePixelsPerTick = vm.Timeline.BasePixelsPerTick;

                double rightPadding = vm.Timeline.RightEmptyPadding;


                // 绝对不允许画面比屏幕窄！(防穿模、防走光)
                newScale = vm.Timeline.ClampZoomScale(newScale, vm.Timeline.ViewportActualWidth);
                // ==============================================================================


                

                // 核心计算公式不变
                double ratio = newScale / oldScale;
                double newOffset = (oldOffset + mouseX) * ratio - mouseX;

                // ================= 【解决频闪的终极黑科技】 =================
                // 🌟 1. 预测新缩放下的物理总宽度
                double expectedNewTotalWidth = vm.Timeline.TotalDurationTicks * basePixelsPerTick * newScale;
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

    

    private readonly HashSet<ScrollViewer> _verticalTrackScrollViewers = new();
    private readonly Dictionary<ItemsControl, ScrollViewer> _itemsControlScrollViewerMap = new();
    // 注册/注销 Vertical ScrollViewer 的 Loaded/Unloaded 处理器
    private void VerticalTrackScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            HookVerticalTrackScrollViewer(scrollViewer);
            return;
        }

        if (sender is ItemsControl itemsControl)
        {
            TryHookItemsControlScrollViewer(itemsControl, deferIfMissing: true);
        }
    }


    private void VerticalTrackScrollViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            UnhookVerticalTrackScrollViewer(scrollViewer);
            return;
        }

        if (sender is ItemsControl itemsControl)
        {
            if (_itemsControlScrollViewerMap.TryGetValue(itemsControl, out var mappedScrollViewer))
            {
                UnhookVerticalTrackScrollViewer(mappedScrollViewer);
                _itemsControlScrollViewerMap.Remove(itemsControl);
            }
        }
    }

    private void TryHookItemsControlScrollViewer(ItemsControl itemsControl, bool deferIfMissing)
    {
        if (_itemsControlScrollViewerMap.ContainsKey(itemsControl))
        {
            return;
        }

        var innerScrollViewer = FindVisualChild<ScrollViewer>(itemsControl);
        if (innerScrollViewer != null)
        {
            _itemsControlScrollViewerMap[itemsControl] = innerScrollViewer;
            HookVerticalTrackScrollViewer(innerScrollViewer);
            return;
        }

        if (deferIfMissing)
        {
            Dispatcher.BeginInvoke(new Action(() => TryHookItemsControlScrollViewer(itemsControl, deferIfMissing: false)), DispatcherPriority.Loaded);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T matched)
            {
                return matched;
            }

            var nested = FindVisualChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void HookVerticalTrackScrollViewer(ScrollViewer scrollViewer)
    {
        _verticalTrackScrollViewers.Add(scrollViewer);
        scrollViewer.ScrollChanged -= VerticalTrackScrollViewer_ScrollChanged;
        scrollViewer.ScrollChanged += VerticalTrackScrollViewer_ScrollChanged;

        GlobalVerticalScroll.Minimum = 0;
        GlobalVerticalScroll.Maximum = scrollViewer.ScrollableHeight;
        GlobalVerticalScroll.ViewportSize = scrollViewer.ViewportHeight;
        GlobalVerticalScroll.SmallChange = 16;
        GlobalVerticalScroll.LargeChange = scrollViewer.ViewportHeight;
    }

    private void UnhookVerticalTrackScrollViewer(ScrollViewer scrollViewer)
    {
        _verticalTrackScrollViewers.Remove(scrollViewer);
        scrollViewer.ScrollChanged -= VerticalTrackScrollViewer_ScrollChanged;
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

    private void TrackItemsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Selector selector && selector.SelectedItem != null)
        {
            selector.SelectedItem = null;
        }
    }

    // 记住拖拽游标前，音乐是否正在播放
    private bool _wasPlayingBeforeDrag = false;
    // 刚捏住游标的一瞬间
    // ================= 游标拖拽逻辑 (Scrubbing) =================

    // ================= 1. 游标 (Playhead) =================

    private double GetTimelineAbsolutePointerX()
    {
        double pointerX = Mouse.GetPosition(OverlayCanvas).X;
        return _timelineInteractionController.ToAbsolutePointerX(pointerX, GlobalHorizontalScroll.Value);
    }

    private void Playhead_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _timelineInteractionController.BeginPlayheadDrag(vm.Timeline, GetTimelineAbsolutePointerX());
        }

        WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

        _dragCoordinator.Start(UpdatePlayheadPosition, enableEdgeAutoScroll: true);
    }




    private void Playhead_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        // 鼠标在屏幕内移动时，也强制触发一次位置更新，保证丝滑
        _dragCoordinator.Pulse();
    }

    private void Playhead_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // 🌟 松手时立刻熄火！
        _dragCoordinator.Stop();

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
            bool isSnapDragging = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            double newSeconds = _timelineInteractionController.ComputePlayheadSeekSeconds(
                vm.Timeline,
                GetTimelineAbsolutePointerX(),
                isSnapDragging);

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
        _marqueePreserveNoteSelection = false;
        Point mousePointInTimelineGrid = e.GetPosition(TimelineMainGrid);
        _marqueeSelectionScope = _marqueeSelectionService.ResolveSelectionScope(current, mousePointInTimelineGrid, TimelineMainGrid, this);
        while (current != null && current != TimelineMainGrid)
        {
            string typeName = current.GetType().Name;

            if (current is FrameworkElement frameworkElement && frameworkElement.Name == "NoteKeyframeEditorPanel")
            {
                _marqueePreserveNoteSelection = true;
            }

            // 如果点到了以下任何交互控件，立刻撤退！把事件完整还给它们！
            if (current is System.Windows.Controls.Primitives.ButtonBase || // 涵盖普通的 Button 和 ToggleButton (下拉箭头)
                current is System.Windows.Controls.Primitives.ScrollBar ||  // 涵盖滚动条
                current is System.Windows.Controls.Primitives.Thumb ||      // 涵盖关键帧小菱形、时间轴红色游标
                current is TextBox ||                                       // 涵盖输入框
                typeName.Contains("DraggableValueBox") ||                   // 涵盖你自定义的数值拖拽框
                typeName.Contains("DraggableOptionBox") ||                  // 涵盖你自定义的选项拖拽框
                typeName.Contains("TimelineRuler") ||                       // 新增这一行！给标尺颁发免死金牌！
                (current is StackPanel sp && (sp.Name == "JudgementLineLayer" || sp.Name == "AudioLayerPanel")))
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
        if (_timelineInteractionController.IsMiddlePanning && e.MiddleButton == MouseButtonState.Pressed)
        {
            Point currentMousePos = e.GetPosition(this);
            double newScrollValue = _timelineInteractionController.ComputeMiddlePanScroll(currentMousePos, GlobalHorizontalScroll.Maximum);

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
                if (DataContext is MainViewModel vm)
                {
                    if (_marqueePreserveNoteSelection)
                    {
                        vm.Timeline.ClearKeyframeSelection();
                        vm.Timeline.ClearLayerSelection();
                    }
                    else
                    {
                        vm.Timeline.ClearAllSelections();
                    }
                }
            }
            return;
        }

        // ================= 终极模式结算 =================

        // 1. 如果什么修饰键都没按（排他框选）：先发广播，清空全场所有选中状态！
        if (_marqueeModifiers == ModifierKeys.None)
        {
            if (DataContext is MainViewModel vm)
            {
                if (_marqueePreserveNoteSelection)
                {
                    vm.Timeline.ClearKeyframeSelection();
                    vm.Timeline.ClearLayerSelection();
                }
                else
                {
                    vm.Timeline.ClearAllSelections();
                }
            }
        }

        GeneralTransform marqueeTransform = MarqueeRect.TransformToAncestor(TimelineMainGrid);
        Rect marqueeBounds = marqueeTransform.TransformBounds(new Rect(0, 0, MarqueeRect.Width, MarqueeRect.Height));

        var selectionRoot = _marqueeSelectionScope ?? TimelineMainGrid;
        var allThumbs = EnumerateIntersectingThumbs(selectionRoot, marqueeBounds);

        foreach (var thumb in allThumbs)
        {
            object? dataContext = thumb.DataContext;
            bool isKeyframeThumb = dataContext is IKeyFrameUiItem;
            bool isNoteThumb = dataContext is NoteViewModel;
            bool isSelectionNode = dataContext is ISelectionNode;

            if (!isSelectionNode || (!isKeyframeThumb && !isNoteThumb))
            {
                continue;
            }

            if (!thumb.IsVisible || !thumb.IsLoaded)
            {
                continue;
            }

            if (dataContext is NoteViewModel noteViewModel && !noteViewModel.ParentTrack.IsExpanded)
            {
                continue;
            }

            if (isKeyframeThumb && IsInNotePropertyKeyframeEditor(thumb) && !IsNotePropertyKeyframeEditorExpanded(thumb))
            {
                continue;
            }

            var selectionNode = (ISelectionNode)dataContext;

            // 3. 根据起手时的修饰键，执行不同的命运
            if (_marqueeModifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl 框选：取反 (Toggle)
                selectionNode.IsSelected = !selectionNode.IsSelected;
            }
            else if (_marqueeModifiers.HasFlag(ModifierKeys.Shift))
            {
                // Shift 框选：纯加选 (Add)
                selectionNode.IsSelected = true;
            }
            else
            {
                // 普通框选 (None)：因为前面已经清空了全场，这里直接点亮即可 (排他)
                selectionNode.IsSelected = true;
            }
        }

        _marqueeSelectionScope = null;
    }

    private IEnumerable<Thumb> EnumerateIntersectingThumbs(DependencyObject selectionRoot, Rect marqueeBounds)
    {
        return _marqueeSelectionService.EnumerateIntersectingThumbs(selectionRoot, marqueeBounds, TimelineMainGrid, this);
    }

    private static bool IsInNotePropertyKeyframeEditor(DependencyObject current)
    {
        return TimelineMarqueeSelectionService.IsInNotePropertyKeyframeEditor(current);
    }

    private static bool IsNotePropertyKeyframeEditorExpanded(DependencyObject current)
    {
        return TimelineMarqueeSelectionService.IsNotePropertyKeyframeEditorExpanded(current);
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
        _dragCoordinator.Start(UpdateRulerDragPosition, enableEdgeAutoScroll: true);

        // 立刻执行一次空降
        _dragCoordinator.Pulse();
        e.Handled = true;
    }

    private void TimelineRuler_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // 鼠标在屏幕内移动时，也强制触发一次位置更新，保证丝滑
        if (e.LeftButton == MouseButtonState.Pressed && MainTimelineRuler.IsMouseCaptured)
        {
            _dragCoordinator.Pulse();
        }
    }

    private void TimelineRuler_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MainTimelineRuler.IsMouseCaptured)
        {
            MainTimelineRuler.ReleaseMouseCapture();
        }

        // 🌟 松手时立刻熄火！
        _dragCoordinator.Stop();

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
            double newSeconds = _timelineInteractionController.ComputeRulerSeekSeconds(
                vm.Timeline,
                GetTimelineAbsolutePointerX());

            vm.Timeline.CurrentPlayTimeSeconds = newSeconds;

            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(newSeconds));
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
    }

    

    // ================= 工作区拖拽逻辑 =================


    // === 左手柄 ===
    private void WorkspaceLeft_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _timelineInteractionController.BeginWorkspaceLeftDrag(vm.Timeline, GetTimelineAbsolutePointerX());
        }

        _dragCoordinator.Start(UpdateWorkspaceLeftPosition, enableEdgeAutoScroll: true);
    }

    private void WorkspaceLeft_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _dragCoordinator.Pulse();
    }

    private void WorkspaceLeft_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) // 记得在 XAML 绑定 DragCompleted 事件
    {
        _dragCoordinator.Stop();
    }

    private void UpdateWorkspaceLeftPosition()
    {
        if (this.DataContext is MainViewModel vm)
        {
            int snappedTick = _timelineInteractionController.ComputeWorkspaceStartTick(
                vm.Timeline,
                GetTimelineAbsolutePointerX());

            vm.Timeline.WorkspaceStartTick = snappedTick;
        }
    }

    // === 右手柄 ===

    // ================= 工作区右手柄拖拽逻辑 =================

    // === 右手柄 ===
    private void WorkspaceRight_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _timelineInteractionController.BeginWorkspaceRightDrag(vm.Timeline, GetTimelineAbsolutePointerX());
        }

        // 将当前动作指定为更新右手柄，并启动 60fps 引擎！
        _dragCoordinator.Start(UpdateWorkspaceRightPosition, enableEdgeAutoScroll: true);
    }

    private void WorkspaceRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        // 只要鼠标在动，就强制触发一次绝对位置计算
        _dragCoordinator.Pulse();
    }

    private void WorkspaceRight_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // 🌟 松手时立刻熄火！(记得在 XAML 里的右侧 Thumb 绑定此事件)
        _dragCoordinator.Stop();
    }

    // 🌟 右手柄位置的绝对计算函数 (被 DragDelta 和 全局计时器 共同调用)
    private void UpdateWorkspaceRightPosition()
    {
        if (this.DataContext is MainViewModel vm)
        {
            int snappedTick = _timelineInteractionController.ComputeWorkspaceEndTick(
                vm.Timeline,
                GetTimelineAbsolutePointerX());

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
            // 防御性设计：如果焦点在输入控件里，千万别拦截，让用户能正常输入。
            if (IsTextInputFocused())
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

        if (IsPlainXKeyPressed(e) && Keyboard.Modifiers == ModifierKeys.None && !IsTextInputFocused())
        {
            if (DataContext is MainViewModel vm && vm.Timeline.DeleteSelectedKeyframesCommand.CanExecute(null))
            {
                vm.Timeline.DeleteSelectedKeyframesCommand.Execute(null);
            }

            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    private static bool IsPlainXKeyPressed(KeyEventArgs e)
    {
        if (e.Key == Key.X || e.ImeProcessedKey == Key.X || e.SystemKey == Key.X || e.DeadCharProcessedKey == Key.X)
        {
            return true;
        }

        // IME 某些状态下会把 Key 折叠成 ImeProcessed/None，物理键状态作为兜底。
        if ((e.Key == Key.ImeProcessed || e.Key == Key.None || e.Key == Key.DeadCharProcessed) && Keyboard.IsKeyDown(Key.X))
        {
            return true;
        }

        return false;
    }

    private static bool IsTextInputFocused()
    {
        if (Keyboard.FocusedElement is TextBox or PasswordBox)
        {
            return true;
        }

        if (Keyboard.FocusedElement is DependencyObject dependencyObject)
        {
            if (FindVisualParent<ComboBox>(dependencyObject) is ComboBox comboBox)
            {
                return comboBox.IsEditable || comboBox.IsKeyboardFocusWithin;
            }
        }

        return false;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
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
            double minScale = vm.Timeline.ComputeMinZoomScale(visiblePixels);
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
        SyncTimelinePanelWidths();
        UpdateMinimapViewport();
    }

    private void TimelineMainGrid_LayoutUpdated(object? sender, EventArgs e)
    {
        SyncTimelinePanelWidths();
    }

    private void SyncTimelinePanelWidths()
    {
        if (TimelineLeftColumnDefinition == null)
        {
            return;
        }

        double width = TimelineLeftColumnDefinition.ActualWidth;
        if (width <= 0 || Math.Abs(width - _lastTimelineLeftPanelWidth) < 0.5)
        {
            return;
        }

        Resources["TimelineLeftPanelWidth"] = new GridLength(width);
        if (Application.Current != null)
        {
            Application.Current.Resources["TimelineLeftPanelWidth"] = new GridLength(width);
        }
        _lastTimelineLeftPanelWidth = width;
    }

    // ================= 【全局缩略图 2：视野框的平移与缩放】 =================

    // === 中间拖拽平移 (Pan) ===
    private void MinimapViewportPan_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) { }

    private void MinimapViewportPan_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            double newValue = _timelineInteractionController.ComputeMinimapPanScroll(
                vm.Timeline,
                e.HorizontalChange,
                GlobalHorizontalScroll.Value,
                GlobalHorizontalScroll.Maximum);

            GlobalHorizontalScroll.Value = newValue; // 联动触发更新
        }
    }

    // === 左侧手柄缩放 (Zoom & Pan) ===
    private void MinimapViewportLeft_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _timelineInteractionController.BeginMinimapViewportResize(vm.Timeline);
        }
    }

    private void MinimapViewportLeft_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm && MainTimelineRuler.ActualWidth > 0)
        {
            var range = _timelineInteractionController.ComputeMinimapViewportLeftResize(vm.Timeline, e.HorizontalChange, minVisibleTicks: 100);
            ApplyViewportChange(vm, range.StartTick, range.EndTick);
        }
    }

    // === 右侧手柄缩放 (Zoom & Pan) ===
    private void MinimapViewportRight_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (this.DataContext is MainViewModel vm)
        {
            _timelineInteractionController.BeginMinimapViewportResize(vm.Timeline);
        }
    }

    private void MinimapViewportRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.DataContext is MainViewModel vm && MainTimelineRuler.ActualWidth > 0)
        {
            var range = _timelineInteractionController.ComputeMinimapViewportRightResize(vm.Timeline, e.HorizontalChange, minVisibleTicks: 100);
            ApplyViewportChange(vm, range.StartTick, range.EndTick);
        }
    }

    // === 核心：将视野的变化转换为底层 ZoomScale 和 滚动条的改变！ ===
    private void ApplyViewportChange(MainViewModel vm, double startTick, double endTick)
    {
        if (!_timelineInteractionController.TryComputeViewportChangeFromMinimapRange(
                vm.Timeline,
                MainTimelineRuler.ActualWidth,
                startTick,
                endTick,
                out var newZoom,
                out var newOffset,
                out var expectedNewMaximum))
        {
            return;
        }

        GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, expectedNewMaximum);
        vm.Timeline.ZoomScale = newZoom;
        GlobalHorizontalScroll.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, newOffset);

        // 强制刷新一次视野！
        UpdateMinimapViewport();
    }

    // ================= 【中键拖拽逻辑 1：按下中键】 =================
    private void TimelineMainGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _timelineInteractionController.BeginMiddlePan(e.GetPosition(this), GlobalHorizontalScroll.Value);

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
        if (e.MiddleButton == MouseButtonState.Released && _timelineInteractionController.IsMiddlePanning)
        {
            _timelineInteractionController.EndMiddlePan();
            TimelineMainGrid.ReleaseMouseCapture();

            // 🌟 新增：恢复默认鼠标指针！
            TimelineMainGrid.Cursor = null;

            e.Handled = true;
        }
    }

    

}