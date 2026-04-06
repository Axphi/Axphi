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

    }
}
