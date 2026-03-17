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
    }
}
