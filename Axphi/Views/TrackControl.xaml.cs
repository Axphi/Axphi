using Axphi.ViewModels;
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


        // 加在类里面，用来记录绝对静止的屏幕坐标
        private Point _trackLastMousePos;


        public TrackControl()
        {
            InitializeComponent();

            // 1. 注册监听全局的滚动指令
            WeakReferenceMessenger.Default.Register<TrackControl, SyncHorizontalScrollMessage>(this, (recipient, message) =>
            {
                // 收到主窗口的指令，立刻让自己的 ScrollViewer 滚到指定位置
                recipient.TrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });

            // 2. 销毁时取消注册，防止内存泄漏
            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        // 3. 原封不动地把鼠标滚轮穿透代码搬过来
        private void InnerTrack_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Shift) return;

            e.Handled = true;

            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            if (sender is UIElement element)
            {
                var parent = VisualTreeHelper.GetParent(element) as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }



        private void KeyframeThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _trackLastMousePos = Mouse.GetPosition(this); // 🌟 取相对于外层绝对静止画板的位置！
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragStarted();
            }
        }

        private void KeyframeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDelta = currentPos.X - _trackLastMousePos.X; // 🌟 自己算稳定位移！
            _trackLastMousePos = currentPos;

            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragDelta(stableDelta); // 丢弃 e.HorizontalChange，传入稳定值！
            }
        }

        private void KeyframeThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragCompleted();
            }
        }

        private void NoteThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _trackLastMousePos = Mouse.GetPosition(this);
            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                if (this.DataContext is TrackViewModel trackVM)
                {
                    trackVM.SelectedNote = noteVM;
                    trackVM.IsNoteExpanded = true;
                }
                noteVM.OnDragStarted();
            }
        }

        private void NoteThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDelta = currentPos.X - _trackLastMousePos.X;
            _trackLastMousePos = currentPos;

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
        private Point _layerLastMousePos;

        private void LayerThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            if (this.DataContext is TrackViewModel trackVM)
            {
                _layerLastMousePos = Mouse.GetPosition(this);
                trackVM.OnLayerDragStarted();
            }
        }

        private void LayerThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDeltaX = currentPos.X - _layerLastMousePos.X;
            _layerLastMousePos = currentPos;

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
            if (DataContext is TrackViewModel trackVM)
            {
                trackVM.HandleLayerPointerDown();
            }
        }

        private void LayerThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TrackViewModel trackVM)
            {
                trackVM.HandleLayerPointerDown();
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
    }
}
