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

        private void KeyframeThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 这个底层的 Preview 事件会在你物理鼠标按下的那一瞬间触发！
            // 不管系统判定 ClickCount 是 1（单击）还是 2（双击），我们统统把它当做一次点击来处理！

            if (sender is FrameworkElement fe)
            {
                // 🌟 使用 dynamic 关键字，完美绕过强类型泛型的限制
                // 这样无论是 KeyFrameUIWrapper<Vector> 还是 KeyFrameUIWrapper<double>，都能顺利拿到命令
                dynamic context = fe.DataContext;
                if (context != null && context!.ToggleSelectionCommand != null)
                {
                    // 立刻执行变色命令！
                    context!.ToggleSelectionCommand.Execute(null);
                }
            }
        }
    }
}
