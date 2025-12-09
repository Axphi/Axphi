using Axphi.Utilities;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Axphi.Components;

public partial class DraggableValueBox : UserControl
{
    public event EventHandler? ValueChanged;

    // 拖拽状态记录
    private Point _startDragScreenPos;
    private double _startDragValue;


    #region Dependency Properties
    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(DraggableValueBox), new PropertyMetadata(string.Empty));
    public string DisplayText
    {
        get { return (string)GetValue(DisplayTextProperty); }
        set { SetValue(DisplayTextProperty, value); }
    }


    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(DraggableValueBox), new PropertyMetadata(0.0));
    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }

    // 2. DragSpeed: 拖拽速度 (默认 0.01)
    public static readonly DependencyProperty DragSpeedProperty = DependencyProperty.Register("DragSpeed", typeof(double), typeof(DraggableValueBox), new PropertyMetadata(0.01));
    public double DragSpeed
    {
        get => (double)GetValue(DragSpeedProperty);
        set => SetValue(DragSpeedProperty, value);
    }

    // 3. Format: 数字格式 (默认 F2)
    public static readonly DependencyProperty NumberFormatProperty = DependencyProperty.Register("NumberFormat", typeof(string), typeof(DraggableValueBox), new PropertyMetadata("F2"));
    public string NumberFormat
    {
        get => (string)GetValue(NumberFormatProperty);
        set => SetValue(NumberFormatProperty, value);
    }

    // 4. Minimum: 最小值 (默认负无穷)
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(DraggableValueBox), new PropertyMetadata(double.MinValue));
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    // 5. Maximum: 最大值 (默认正无穷)
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(DraggableValueBox), new PropertyMetadata(double.MaxValue));
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(DraggableValueBox), new PropertyMetadata(false, IsEditableChanged));
    public bool IsEditable
    {
        get { return (bool)GetValue(IsEditableProperty); }
        set { SetValue(IsEditableProperty, value); }
    }

    private static readonly DependencyPropertyKey IsDraggingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(DraggableValueBox), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;

    public bool IsDragging
    {
        get => (bool)GetValue(IsDraggingProperty); 
        private set => SetValue(IsDraggingPropertyKey, value);
    }

    public DraggableValueBox()
    {
        InitializeComponent();
    }

    private static void IsEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //if (d is not DraggableValueBox obj)
        //{
        //    return;
        //}

        //var oldValue = (bool)e.OldValue;
        //var newValue = (bool)e.NewValue;

        //if (!oldValue && newValue)
        //{
        //    obj.EnterEditMode();
        //}
        //else
        //{
        //    obj.FinishEdit();
        //}
    }



    #endregion


    // --- 交互逻辑：鼠标按下 ---
    private void Number_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement frameworkElement)
        {
            _startDragScreenPos = e.GetPosition(frameworkElement);
            _startDragValue = Value;
            IsDragging = true;

            Mouse.OverrideCursor = Cursors.SizeWE;
            Container.CaptureMouse();
        }
    }

    // --- 交互逻辑：鼠标拖动 (核心) ---
    private void Number_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDragging || sender is not FrameworkElement frameworkElement)
        {
            return;
        }

        Point currentScreenPos = frameworkElement.PointToScreen(e.GetPosition(frameworkElement));
        double deltaX = currentScreenPos.X - _startDragScreenPos.X;

        // 如果移动距离太小，视为静止，防抖动
        if (Math.Abs(deltaX) < 2)
            return;

        // 1. 计算倍率
        double speed = DragSpeed;

        // Shift: 微调 (速度 * 0.1)
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            speed *= 0.1;

        // Ctrl: 吸附 (这里实现为加速 * 10，或者你可以改成取整逻辑)
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            speed *= 10.0;

        // 2. 计算新值
        double newValue = _startDragValue + (deltaX * speed);

        // 【新增代码】: 实施数值钳制 (Clamp)
        // 如果小于 Min，就死死定在 Min；如果大于 Max，就死死定在 Max
        if (newValue < Minimum) newValue = Minimum;
        if (newValue > Maximum) newValue = Maximum;


        // Ctrl 吸附逻辑 (可选：吸附到整数)
        // if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        //    newValue = Math.Round(newValue);

        // 3. 更新界面
        DisplayText = newValue.ToString(NumberFormat);
        Value = newValue;

        // --- 4. 屏幕边缘回环 (Infinite Wrap) ---
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        bool warped = false;
        Point newPos = currentScreenPos;

        if (currentScreenPos.X <= 2) // 碰到左边缘
        {
            newPos.X = screenWidth - 2;
            warped = true;
        }
        else if (currentScreenPos.X >= screenWidth - 2) // 碰到右边缘
        {
            newPos.X = 2;
            warped = true;
        }

        if (warped)
        {
            SetCursorPos((int)newPos.X, (int)newPos.Y);
            // 重要：修正起始点，防止数值跳变
            _startDragScreenPos = new Point(newPos.X - deltaX, _startDragScreenPos.Y);
        }
    }

    // --- 交互逻辑：鼠标抬起 ---
    private void Number_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsDragging) return;

        IsDragging = false;
        Container.ReleaseMouseCapture();
        Mouse.OverrideCursor = null; // 恢复光标
        //EnterEditMode();
    }

    // --- 编辑模式逻辑 ---
    private void EnterEditMode()
    {
        //// 找到里面的 TextBox 和 TextBlock
        //var textBox = FindVisualChild<TextBox>(Container);
        //var border = FindVisualChild<Border>(Container);

        //if (textBox != null && border != null)
        //{
        //    border.Visibility = Visibility.Collapsed;
        //    textBox.Visibility = Visibility.Visible;
        //    textBox.Focus();
        //    textBox.SelectAll();

        //    // 记录当前编辑框，并开始监听窗口全局点击
        //    var parentWindow = Window.GetWindow(this);
        //    if (parentWindow != null)
        //    {
        //        parentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
        //    }
        //    IsEditable = true;
        //}
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            //FinishEdit();
        }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        //FinishEdit();
    }

    //private void FinishEdit()
    //{
    //    // 1. 尝试解析输入的值
    //    if (double.TryParse(DisplayText, out double val))
    //    {
    //        Value = val;
    //        DisplayText = val.ToString(NumberFormat);
    //    }

    //    // 编辑结束，移除窗口监听，清空引用
    //    var parentWindow = Window.GetWindow(this);
    //    if (parentWindow != null)
    //    {
    //        parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
    //    }
    //    IsEditable = false;
    //}

    // 【新增】全局点击拦截
    private void ParentWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        //var clickedElement = e.OriginalSource as DependencyObject;

        //if (IsChildOf(_currentEditingTextBox, clickedElement))
        //{
        //    return;
        //}

        //FinishEdit();
    }

    // 辅助方法：判断 clicked 是否在 parent 内部
    private bool IsChildOf(DependencyObject parent, DependencyObject clicked)
    {
        while (clicked != null)
        {
            if (clicked == parent) return true;
            clicked = VisualTreeHelper.GetParent(clicked);
        }
        return false;
    }

    //// 辅助查找子控件
    //private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    //{
    //    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
    //    {
    //        var child = VisualTreeHelper.GetChild(parent, i);
    //        if (child is T t) return t;
    //        var result = FindVisualChild<T>(child);
    //        if (result != null) return result;
    //    }
    //    return null;
    //}



    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int X, int Y);
}

