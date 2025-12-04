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
    /// ClickToEditTextBox.xaml 的交互逻辑
    /// </summary>
    public partial class ClickToEditTextBox : UserControl
    {

        // 定义一个事件：当数值提交并发生改变时触发
        public event EventHandler? ValueChanged;
        public ClickToEditTextBox()
        {
            InitializeComponent();
        }

        // --- 依赖属性 (Dependency Property) ---
        // 让我们可以像使用原生控件一样绑定 Text="{Binding ...}"
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(ClickToEditTextBox), new PropertyMetadata(string.Empty));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // --- 1. 进入编辑模式 ---
        private void DisplayBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 还没进入编辑模式时，把 TextBlock 的值同步给 TextBox
            InputBox.Text = Text;

            DisplayBlock.Visibility = Visibility.Hidden;
            InputBox.Visibility = Visibility.Visible;

            InputBox.Focus();
            InputBox.SelectAll();

            // 【关键】开始监听整个窗口的点击，用来实现“点击别处提交”
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
            }

            e.Handled = true; // 阻止事件冒泡
        }

        // --- 2. 提交并退出编辑模式 ---
        private void CommitAndClose()
        {
            if (InputBox.Visibility != Visibility.Visible) return;

            // 更新绑定的 Text 属性
            Text = InputBox.Text;

            // 触发 ValueChanged 事件通知外部
            ValueChanged?.Invoke(this, EventArgs.Empty);

            // 切换 UI
            InputBox.Visibility = Visibility.Hidden;
            DisplayBlock.Visibility = Visibility.Visible;

            // 移除窗口监听，释放资源
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
            }

            // 清除焦点
            Keyboard.ClearFocus();
        }

        // --- 3. 各种提交触发时机 ---

        // 回车键
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitAndClose();
                e.Handled = true; // 防止回车继续传给父控件
            }
        }

        // 失去焦点
        private void InputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitAndClose();
        }

        // 【全局点击拦截】逻辑搬到这里来了
        private void ParentWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var clickedElement = e.OriginalSource as DependencyObject;

            // 如果点的是我自己(输入框内部)，啥也不做
            if (IsChildOf(clickedElement, InputBox))
            {
                return;
            }

            // 如果点的是外面，提交！
            CommitAndClose();
        }

        // 辅助方法
        private bool IsChildOf(DependencyObject child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent) return true;
                child = VisualTreeHelper.GetParent(child);
            }
            return false;
        }
    }
}
