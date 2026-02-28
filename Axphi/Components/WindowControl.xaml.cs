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

namespace Axphi.Components
{
    /// <summary>
    /// WindowControl.xaml 的交互逻辑
    /// </summary>
    public partial class WindowControl : UserControl
    {
        public WindowControl()
        {
            InitializeComponent();
        }

        // 最小化
        private void MinimizeSelf_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this); // 找到包含这个控件的父窗口
            if (window != null) window.WindowState = WindowState.Minimized;
        }

        // 最大化/还原
        private void MaximizeRestoreSelf_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        }

        // 关闭
        private void CloseSelf_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window?.Close(); // 拿到窗口实例，执行关闭
        }
    }
}
