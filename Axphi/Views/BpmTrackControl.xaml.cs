using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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

        private readonly HorizontalDragTracker _dragTracker = new();

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
            MouseWheelPassthrough.TryHandle(sender as UIElement, e);
        }


        // 2. 替换拖拽起手事件
        private void KeyframeThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // 🌟 取相对于外层绝对静止画板的位置！
            _dragTracker.Start(this);

            if (sender is FrameworkElement fe && fe.DataContext is ITimelineDraggable draggable)
            {
                draggable.OnDragStarted();
            }
        }

        // 3. 替换拖拽中事件
        private void KeyframeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            // 🌟 自己算稳定位移！
            double stableDelta = _dragTracker.GetDeltaX(this);

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
    }
}
