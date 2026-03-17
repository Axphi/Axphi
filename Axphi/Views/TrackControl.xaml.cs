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
            Debug.WriteLine("KeyframeThumb_DragStarted 被调用");
            // 只要 DataContext 不是空的，我们就大胆把它变成 dynamic
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragStarted();
            }
        }

        private void KeyframeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragDelta(e.HorizontalChange);
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
            Debug.WriteLine("NoteThumb_DragStarted 被调用");

            // 直接强转为 NoteViewModel，比 dynamic 更安全高效！
            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                // ⭐ 核心重点：告诉外层的 Track 大管家，现在选中的是这个音符！
                // 这样左侧的 Note 属性面板就会瞬间绑定到这个音符的数据上
                if (this.DataContext is TrackViewModel trackVM)
                {
                    trackVM.SelectedNote = noteVM;
                    // ✨ 【新增这行神仙代码】：强行把左侧的 ">" 变成 "v"
                    trackVM.IsNoteExpanded = true;
                }

                // 调用 NoteViewModel 内部的拖拽起手式
                noteVM.OnDragStarted();
            }
        }

        private void NoteThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel noteVM)
            {
                // 把鼠标的位移量传给 ViewModel 让它自己算时间去
                noteVM.OnDragDelta(e.HorizontalChange);
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
                // 1. 获取外层的大管家
                if (this.DataContext is TrackViewModel trackVM)
                {
                    var timeline = trackVM._timeline; // 确保你能拿到 TimelineViewModel 的实例

                    // 2. 计算拖拽后的新像素宽度
                    double newPixelWidth = noteVM.UIHoldPixelWidth + e.HorizontalChange;
                    if (newPixelWidth < 0) newPixelWidth = 0; // 不能变负数！

                    // 3. 把像素宽度完美反推回 Tick！(直接复用你写好的公式)
                    double exactTicks = timeline.PixelToTick(newPixelWidth);
                    int newHoldDurationTicks = (int)Math.Round(exactTicks, MidpointRounding.AwayFromZero);

                    // 4. 赋值给 ViewModel（它会自动触发 OnHoldDurationChanged 拦截器去更新画面！）
                    noteVM.HoldDuration = newHoldDurationTicks;

                }
            }
        }
    }
}
