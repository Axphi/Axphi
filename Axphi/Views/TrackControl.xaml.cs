using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Axphi.Views
{
    /// <summary>
    /// TrackControl.xaml 的交互逻辑
    /// </summary>
    public partial class TrackControl : UserControl
    {

        private readonly HorizontalDragTracker _keyframeDragTracker = new();
        private readonly HorizontalDragTracker _layerDragTracker = new();
        private bool _isParentLinkDragging;
        private Window? _parentLinkWindow;


        public TrackControl()
        {
            InitializeComponent();

            Loaded += (_, _) => ApplyCurrentHorizontalOffset();

            // 1. 注册监听全局的滚动指令
            WeakReferenceMessenger.Default.Register<TrackControl, SyncHorizontalScrollMessage>(this, (recipient, message) =>
            {
                // 收到主窗口的指令，立刻让自己的 ScrollViewer 滚到指定位置
                recipient.TrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });

            // 2. 销毁时取消注册，防止内存泄漏
            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void ApplyCurrentHorizontalOffset()
        {
            if (DataContext is TrackViewModel trackViewModel)
            {
                TrackScrollViewer.ScrollToHorizontalOffset(trackViewModel.Timeline.CurrentHorizontalScrollOffset);
            }
        }

        // 3. 原封不动地把鼠标滚轮穿透代码搬过来
        private void InnerTrack_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MouseWheelPassthrough.TryHandle(sender as UIElement, e);
        }



        private void KeyframeThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _keyframeDragTracker.Start(this);
            if (sender is FrameworkElement fe && fe.DataContext is ITimelineDraggable draggable)
            {
                draggable.OnDragStarted();
            }
        }

        private void KeyframeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double stableDelta = _keyframeDragTracker.GetDeltaX(this);

            if (sender is FrameworkElement fe && fe.DataContext is ITimelineDraggable draggable)
            {
                draggable.OnDragDelta(stableDelta);
            }
        }

        private void KeyframeThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ITimelineDraggable draggable)
            {
                draggable.OnDragCompleted();
            }
        }

        private void KeyframeThumb_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext == null)
            {
                return;
            }

            if (IsInNoteKeyframeEditorPanel(fe))
            {
                return;
            }

            if (fe.DataContext is IRightClickableTimelineItem item)
            {
                item.OnRightClick();
                e.Handled = true;
            }
        }

        private void ExpressionIndicator_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is TrackExpressionSlot slot)
            {
                slot.IsEnabled = !slot.IsEnabled;
                e.Handled = true;
            }
        }

        private void ExpressionEditorTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitExpressionEditor(sender as FrameworkElement);
        }

        private void ExpressionEditorTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                CommitExpressionEditor(sender as FrameworkElement);
                e.Handled = true;
            }
        }

        private static void CommitExpressionEditor(FrameworkElement? element)
        {
            if (element?.DataContext is TrackExpressionSlot slot)
            {
                slot.CommitNow();
            }
        }

        private static bool IsInNoteKeyframeEditorPanel(DependencyObject current)
        {
            while (current != null)
            {
                if (current is FrameworkElement element && element.Name == "NoteKeyframeEditorPanel")
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void NoteThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _keyframeDragTracker.Start(this);
            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                if (this.DataContext is TrackViewModel trackVM)
                {
                    trackVM.IsNoteExpanded = true;
                }
                noteVM.OnDragStarted();
                noteVM.ParentTrack.Timeline.RefreshNoteSelectionState(noteVM.ParentTrack, noteVM);
            }
        }

        private void NoteThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double stableDelta = _keyframeDragTracker.GetDeltaX(this);

            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                noteVM.OnDragDelta(stableDelta); // 丢弃 e.HorizontalChange，传入稳定值！
            }
        }

        private void NoteThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                noteVM.OnDragCompleted();
            }
        }


        // 拖拽 Hold 尾巴，改变长度
        private void HoldTail_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                if (this.DataContext is TrackViewModel trackVM)
                {
                    var timeline = trackVM._timeline;

                    double newPixelWidth = noteVM.UIHoldPixelWidth + e.HorizontalChange;
                    if (newPixelWidth < 0) newPixelWidth = 0;

                    double exactTicks = timeline.PixelToTick(newPixelWidth);

                    // 1. 算出尾巴在宇宙中的绝对时间
                    double exactTailTickDouble = noteVM.Model.HitTime + exactTicks;

                    // 2. 🌟 召唤吸附！尾巴会自动去寻找附近的小节线或关键帧
                    int snappedTailTick = timeline.SnapToClosest(exactTailTickDouble);

                    // 3. 把吸附后的绝对时间，重新减去头部时间，算回真实长度
                    int newHoldDurationTicks = Math.Max(0, snappedTailTick - noteVM.Model.HitTime);

                    // 赋值给 ViewModel (ViewModel里的拦截器会自动帮你刷新画面宽度的！)
                    noteVM.HoldDuration = newHoldDurationTicks;
                }
            }
        }


        // ================= 图层块 (LayerRect) 实时拖拽逻辑 =================

        private void LayerThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            if (this.DataContext is TrackViewModel trackVM)
            {
                _layerDragTracker.Start(this);
                trackVM.OnLayerDragStarted();
            }
        }

        private void LayerThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double stableDeltaX = _layerDragTracker.GetDeltaX(this);

            if (this.DataContext is TrackViewModel trackVM)
            {
                trackVM.OnLayerDragDelta(stableDeltaX);
            }
        }

        private void LayerThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.DataContext is TrackViewModel trackVM)
            {
                trackVM.OnLayerDragCompleted();
            }
        }

        private void LayerHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsFromParentBindingUi(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (DataContext is TrackViewModel trackVM)
            {
                if (e.ClickCount == 2)
                {
                    trackVM.EnterJudgementLineEditorCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                trackVM.HandleLayerPointerDown();
            }
        }

        private void LayerHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsFromParentBindingUi(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (DataContext is TrackViewModel trackVM)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    return;
                }

                trackVM.HandleLayerPointerUp();
            }
        }

        private void LayerThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TrackViewModel trackVM)
            {
                if (e.ClickCount == 2)
                {
                    trackVM.EnterJudgementLineEditorCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

                trackVM.HandleLayerPointerDown();
            }
        }

        private void LayerThumb_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TrackViewModel trackVM)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    return;
                }

                trackVM.HandleLayerPointerUp();
            }
        }


        // ================= 图层块左边缘修剪逻辑 =================
        private Point _layerLeftLastMousePos;
        private double _layerLeftVirtualX;
        private double _layerLeftVirtualWidth;

        private void LayerLeftHandle_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _layerLeftLastMousePos = Mouse.GetPosition(this);
            if (this.DataContext is TrackViewModel trackVM)
            {
                _layerLeftVirtualX = trackVM.LayerPixelXOffset;
                _layerLeftVirtualWidth = trackVM.LayerPixelWidth;
            }
            e.Handled = true; // 🌟 极其重要：防止触发底下的整体移动！
        }

        private void LayerLeftHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDeltaX = currentPos.X - _layerLeftLastMousePos.X;
            _layerLeftLastMousePos = currentPos;

            if (this.DataContext is TrackViewModel trackVM)
            {
                _layerLeftVirtualX += stableDeltaX;
                _layerLeftVirtualWidth -= stableDeltaX;

                // 🌟 核心：算出 1 个 Tick 对应的物理像素，作为视觉和物理的双重防撞墙
                double minWidthPixel = trackVM._timeline.TickToPixel(1);

                // 视觉防撞墙：最少保留 1 Tick 的宽度
                if (_layerLeftVirtualWidth < minWidthPixel)
                {
                    _layerLeftVirtualX += _layerLeftVirtualWidth - minWidthPixel;
                    _layerLeftVirtualWidth = minWidthPixel;
                }

                double exactLeftTick = trackVM._timeline.PixelToTick(_layerLeftVirtualX);
                int snappedLeftTick = trackVM._timeline.SnapToClosest(exactLeftTick, isPlayhead: false);

                int rightTick = trackVM.LayerStartTick + trackVM.LayerDurationTicks; // 右边界是绝对死锁的


                // 逻辑防撞墙：吸附后的 Tick 绝不能越过 (右边界 - 1)
                if (snappedLeftTick > rightTick - 1)
                    snappedLeftTick = rightTick - 1;


                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                {
                    trackVM.LayerPixelXOffset = trackVM._timeline.TickToPixel(snappedLeftTick);
                    trackVM.LayerPixelWidth = trackVM._timeline.TickToPixel(rightTick - snappedLeftTick);

                    // 🌟 1. 实时更新底层寿命 (磁吸态)
                    trackVM.Data.StartTick = snappedLeftTick;
                    trackVM.Data.DurationTicks = rightTick - snappedLeftTick;

                }
                else
                {
                    trackVM.LayerPixelXOffset = _layerLeftVirtualX;
                    trackVM.LayerPixelWidth = _layerLeftVirtualWidth;

                    // 🌟 2. 实时更新底层寿命 (丝滑平滑态，算出一个临时的准确 Tick 给渲染器)
                    int tempStartTick = (int)Math.Round(exactLeftTick, MidpointRounding.AwayFromZero);
                    if (tempStartTick > rightTick - 1) tempStartTick = rightTick - 1;

                    trackVM.Data.StartTick = tempStartTick;
                    trackVM.Data.DurationTicks = rightTick - tempStartTick;

                }
                // 🌟 3. 发送重绘信件，让右边的画面实时跟着修剪！
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }

            
            e.Handled = true;
        }

        private void LayerLeftHandle_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.DataContext is TrackViewModel trackVM)
            {
                double exactLeftTick = trackVM._timeline.PixelToTick(trackVM.LayerPixelXOffset);
                int finalLeftTick = (int)Math.Round(exactLeftTick, MidpointRounding.AwayFromZero);

                int rightTick = trackVM.LayerStartTick + trackVM.LayerDurationTicks;

                // 🌟 修剪边缘不属于“全员搬家”，只更新图层的壳子即可！
                trackVM.LayerStartTick = finalLeftTick;
                trackVM.LayerDurationTicks = Math.Max(1, rightTick - finalLeftTick);

                trackVM.LayerPixelXOffset = trackVM._timeline.TickToPixel(trackVM.LayerStartTick);
                trackVM.LayerPixelWidth = trackVM._timeline.TickToPixel(trackVM.LayerDurationTicks);


                trackVM.Data.StartTick = trackVM.LayerStartTick; // 🌟 【新增】同步给底层！
                trackVM.Data.DurationTicks = trackVM.LayerDurationTicks; // 🌟 【新增】同步给底层！
            }
            e.Handled = true;
        }

        // ================= 图层块右边缘修剪逻辑 =================
        private Point _layerRightLastMousePos;
        private double _layerRightVirtualWidth;

        private void LayerRightHandle_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _layerRightLastMousePos = Mouse.GetPosition(this);
            if (this.DataContext is TrackViewModel trackVM)
            {
                _layerRightVirtualWidth = trackVM.LayerPixelWidth;
            }
            e.Handled = true;
        }

        private void LayerRightHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDeltaX = currentPos.X - _layerRightLastMousePos.X;
            _layerRightLastMousePos = currentPos;

            if (this.DataContext is TrackViewModel trackVM)
            {
                _layerRightVirtualWidth += stableDeltaX;

                // 🌟 视觉防撞墙：最小像素宽度为 1 Tick
                double minWidthPixel = trackVM._timeline.TickToPixel(1);
                if (_layerRightVirtualWidth < minWidthPixel)
                    _layerRightVirtualWidth = minWidthPixel;

                double exactRightTick = trackVM.LayerStartTick + trackVM._timeline.PixelToTick(_layerRightVirtualWidth);
                int snappedRightTick = trackVM._timeline.SnapToClosest(exactRightTick, isPlayhead: false);

                int newDurationTicks = snappedRightTick - trackVM.LayerStartTick;

                // 🌟 逻辑防撞墙：最小允许 1 Tick 长度
                if (newDurationTicks < 1)
                    newDurationTicks = 1;


                if (newDurationTicks < 1) newDurationTicks = 1; // 最小允许 1 tick 长度

                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                {
                    trackVM.LayerPixelWidth = trackVM._timeline.TickToPixel(newDurationTicks);
                    // 🌟 1. 实时更新底层寿命 (磁吸态)
                    trackVM.Data.DurationTicks = newDurationTicks;
                }
                else
                {
                    trackVM.LayerPixelWidth = _layerRightVirtualWidth;
                    // 🌟 2. 实时更新底层寿命 (丝滑平滑态)
                    int tempDuration = (int)Math.Round(exactRightTick - trackVM.LayerStartTick, MidpointRounding.AwayFromZero);
                    if (tempDuration < 1) tempDuration = 1;

                    trackVM.Data.DurationTicks = tempDuration;
                }
                // 🌟 3. 发送重绘信件，让右侧画面实时响应！
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            e.Handled = true;
        }

        private void LayerRightHandle_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.DataContext is TrackViewModel trackVM)
            {
                double exactRightTick = trackVM.LayerStartTick + trackVM._timeline.PixelToTick(trackVM.LayerPixelWidth);
                int finalRightTick = (int)Math.Round(exactRightTick, MidpointRounding.AwayFromZero);

                trackVM.LayerDurationTicks = Math.Max(1, finalRightTick - trackVM.LayerStartTick);
                
                trackVM.LayerPixelWidth = trackVM._timeline.TickToPixel(trackVM.LayerDurationTicks);
                trackVM.Data.DurationTicks = trackVM.LayerDurationTicks; // 🌟 【新增】同步给底层！
            }
            e.Handled = true;
        }

        private void ParentLinkHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not TrackViewModel sourceTrack)
            {
                return;
            }

            _isParentLinkDragging = true;

            _parentLinkWindow = Window.GetWindow(this);
            if (_parentLinkWindow != null)
            {
                Point startPoint = ParentLinkHandle.TranslatePoint(
                    new Point(ParentLinkHandle.ActualWidth / 2.0, ParentLinkHandle.ActualHeight / 2.0),
                    _parentLinkWindow);
                WeakReferenceMessenger.Default.Send(new ParentBindingDragStartedMessage(sourceTrack.Data.ID, startPoint));

                _parentLinkWindow.PreviewMouseMove += ParentLinkWindow_PreviewMouseMove;
                _parentLinkWindow.PreviewMouseLeftButtonUp += ParentLinkWindow_PreviewMouseLeftButtonUp;
            }

            Mouse.Capture(this, CaptureMode.SubTree);
            e.Handled = true;
        }

        private void ParentLinkWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isParentLinkDragging || _parentLinkWindow == null || DataContext is not TrackViewModel sourceTrack)
            {
                return;
            }

            Point currentPoint = e.GetPosition(_parentLinkWindow);
            WeakReferenceMessenger.Default.Send(new ParentBindingDragUpdatedMessage(sourceTrack.Data.ID, currentPoint));
        }

        private void ParentLinkWindow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isParentLinkDragging)
            {
                return;
            }

            if (_parentLinkWindow != null && DataContext is TrackViewModel sourceTrack)
            {
                Point endPoint = e.GetPosition(_parentLinkWindow);
                WeakReferenceMessenger.Default.Send(new ParentBindingDragCompletedMessage(sourceTrack.Data.ID, endPoint));
            }

            EndParentLinkDrag();
        }

        private void ParentBindingOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || DataContext is not TrackViewModel trackViewModel)
            {
                return;
            }

            string? parentLineId = button.Tag as string;
            trackViewModel.Timeline.TrySetParentLine(trackViewModel, string.IsNullOrWhiteSpace(parentLineId) ? null : parentLineId);
            ParentBindingToggleButton.IsChecked = false;
            e.Handled = true;
        }

        private void EndParentLinkDrag()
        {
            _isParentLinkDragging = false;

            if (_parentLinkWindow != null)
            {
                _parentLinkWindow.PreviewMouseMove -= ParentLinkWindow_PreviewMouseMove;
                _parentLinkWindow.PreviewMouseLeftButtonUp -= ParentLinkWindow_PreviewMouseLeftButtonUp;
                _parentLinkWindow = null;
            }

            if (Mouse.Captured == this)
            {
                Mouse.Capture(null);
            }
        }

        private static bool IsFromParentBindingUi(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is FrameworkElement element)
                {
                    if (element.Name == "ParentLinkHandle" || element.Name == "ParentBindingToggleButton")
                    {
                        return true;
                    }
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }
    }
}
