using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
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
    /// BpmTrackControl.xaml 的交互逻辑
    /// </summary>
    public partial class BpmTrackControl : UserControl
    {

        // 1. 在类里加一个全局变量，记录绝对静止的屏幕坐标
        private Point _bpmLastMousePos;

        public BpmTrackControl()
        {
            InitializeComponent();

            Loaded += (_, _) => ApplyCurrentHorizontalOffset();

            // 监听主窗口滚动条传来的滚动指令
            WeakReferenceMessenger.Default.Register<BpmTrackControl, SyncHorizontalScrollMessage>(this, (recipient, message) =>
            {
                recipient.BpmTrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });

            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void ApplyCurrentHorizontalOffset()
        {
            if (DataContext is BpmTrackViewModel bpmTrackViewModel)
            {
                BpmTrackScrollViewer.ScrollToHorizontalOffset(bpmTrackViewModel.Timeline.CurrentHorizontalScrollOffset);
            }
        }

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


        // 2. 替换拖拽起手事件
        private void KeyframeThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // 🌟 取相对于外层绝对静止画板的位置！
            _bpmLastMousePos = Mouse.GetPosition(this);

            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragStarted();
            }
        }

        // 3. 替换拖拽中事件
        private void KeyframeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            // 🌟 自己算稳定位移！
            Point currentPos = Mouse.GetPosition(this);
            double stableDelta = currentPos.X - _bpmLastMousePos.X;
            _bpmLastMousePos = currentPos;

            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                // 🌟 丢弃自带的 e.HorizontalChange，传入我们算好的稳定值！
                wrapper.OnDragDelta(stableDelta);
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
    }
}
