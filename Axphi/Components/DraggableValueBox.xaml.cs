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
    private Point _lastDragScreenPos;
    private Window? _parentWindow;


    #region Dependency Properties
    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(DraggableValueBox), new PropertyMetadata(string.Empty));
    public string DisplayText
    {
        get { return (string)GetValue(DisplayTextProperty); }
        set { SetValue(DisplayTextProperty, value); }
    }


    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(DraggableValueBox),
        new FrameworkPropertyMetadata(
            0.0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged,
            CoerceValue));
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
    public static readonly DependencyProperty NumberFormatProperty = DependencyProperty.Register(
        "NumberFormat",
        typeof(string),
        typeof(DraggableValueBox),
        new PropertyMetadata("F2", OnNumberFormatChanged));
    public string NumberFormat
    {
        get => (string)GetValue(NumberFormatProperty);
        set => SetValue(NumberFormatProperty, value);
    }

    // 4. Minimum: 最小值 (默认负无穷)
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        "Minimum",
        typeof(double),
        typeof(DraggableValueBox),
        new PropertyMetadata(double.MinValue, OnMinMaxChanged));
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    // 5. Maximum: 最大值 (默认正无穷)
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        "Maximum",
        typeof(double),
        typeof(DraggableValueBox),
        new PropertyMetadata(double.MaxValue, OnMinMaxChanged));
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

        Loaded += (_, _) => SyncDisplayText();
        Unloaded += (_, _) => UnhookWindowClick();
    }

    private static void IsEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DraggableValueBox obj)
        {
            return;
        }

        var newValue = (bool)e.NewValue;
        if (newValue)
        {
            obj.BeginEdit();
        }
        else
        {
            obj.EndEdit();
        }
    }



    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DraggableValueBox obj)
        {
            return;
        }

        obj.SyncDisplayText();
        obj.ValueChanged?.Invoke(obj, EventArgs.Empty);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        if (d is not DraggableValueBox obj)
        {
            return baseValue;
        }

        var value = (double)baseValue;
        if (value < obj.Minimum) value = obj.Minimum;
        if (value > obj.Maximum) value = obj.Maximum;
        return value;
    }

    private static void OnMinMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DraggableValueBox obj)
        {
            return;
        }

        // 当 Min/Max 改变时，强制重新钳制当前 Value
        obj.CoerceValue(ValueProperty);
        obj.SyncDisplayText();
    }

    private void SyncDisplayText()
    {
        try
        {
            DisplayText = Value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            // NumberFormat 非法时，退回默认格式，避免 UI 直接炸
            DisplayText = Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    #endregion

    private static void OnNumberFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DraggableValueBox obj)
        {
            return;
        }

        obj.SyncDisplayText();
    }


    // --- 交互逻辑：鼠标按下 ---
    private void Number_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsEditable)
        {
            return;
        }

        // 双击进入编辑模式（不触发拖拽）
        if (e.ClickCount >= 2)
        {
            IsEditable = true;
            e.Handled = true;
            return;
        }

        var cursor = GetCursorPosPixels();
        _lastDragScreenPos = new Point(cursor.X, cursor.Y);
        IsDragging = true;

        Mouse.OverrideCursor = Cursors.SizeWE;
        Container.CaptureMouse();
    }

    // --- 交互逻辑：鼠标拖动 (核心) ---
    private void Number_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDragging)
        {
            return;
        }

        // 用真实光标像素坐标（避免 PointToScreen 在捕获/缩放下偶发不准）
        var cursor = GetCursorPosPixels();
        Point currentScreenPos = new Point(cursor.X, cursor.Y);

        // 基于“上一帧”计算增量，避免回环/瞬移导致 delta 巨大而数值跳变
        double deltaX = currentScreenPos.X - _lastDragScreenPos.X;

        // --- 4. 屏幕边缘回环 (Infinite Wrap) ---
        // 使用虚拟屏幕像素边界（多显示器），并在回环时重置基准点。
        var bounds = GetVirtualScreenBoundsPixels();
        var boundsWidth = bounds.Right - bounds.Left;

        // 如果系统/驱动导致光标瞬移（例如 SetCursorPos 尚未生效的一帧），
        // 这一帧直接丢弃并重置基准，防止 Value 大跳。
        if (boundsWidth > 0 && Math.Abs(deltaX) > boundsWidth * 0.5)
        {
            _lastDragScreenPos = currentScreenPos;
            return;
        }

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

        // 2. 计算新值（增量式：基于当前 Value）
        double newValue = Value + (deltaX * speed);


        // Ctrl 吸附逻辑 (可选：吸附到整数)
        // if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        //    newValue = Math.Round(newValue);

        // 3. 更新界面（增量式更新）
        Value = newValue; // Value 会被 CoerceValue 钳制，并在 OnValueChanged 同步 DisplayText

        // 先更新基准点（正常情况下一帧一帧推进）
        _lastDragScreenPos = currentScreenPos;

        bool warped = false;
        Point newPos = currentScreenPos;

        const int edgePadding = 4;
        if (currentScreenPos.X <= bounds.Left + edgePadding)
        {
            newPos.X = bounds.Right - edgePadding;
            warped = true;
        }
        else if (currentScreenPos.X >= bounds.Right - edgePadding)
        {
            newPos.X = bounds.Left + edgePadding;
            warped = true;
        }

        if (warped)
        {
            SetCursorPos((int)newPos.X, (int)newPos.Y);

            // 回环后立刻把基准点设为目标位置，避免下一帧出现巨额 delta
            _lastDragScreenPos = newPos;
            return;
        }
    }

    // --- 交互逻辑：鼠标抬起 ---
    private void Number_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsDragging) return;

        IsDragging = false;
        Container.ReleaseMouseCapture();
        Mouse.OverrideCursor = null; // 恢复光标

        // 如果几乎没移动，把它当点击（可选：单击编辑）。这里保守一点不自动进编辑，避免误触。
    }

    // --- 编辑模式逻辑 ---
    private void BeginEdit()
    {
        // 进入编辑时，编辑框显示“当前显示文本”，避免 double 精度尾巴
        EditBox.Text = DisplayText;

        HookWindowClick();

        // Focus 需要等可见性切换后执行，Dispatcher 更稳
        Dispatcher.BeginInvoke(new Action(() =>
        {
            EditBox.Focus();
            EditBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void EndEdit()
    {
        UnhookWindowClick();
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FinishEdit(commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FinishEdit(commit: false);
            e.Handled = true;
        }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (IsEditable)
        {
            FinishEdit(commit: true);
        }
    }

    //private void FinishEdit(bool commit)
    //{
    //    if (!IsEditable)
    //    {
    //        return;
    //    }

    //    if (commit)
    //    {
    //        var text = (EditBox.Text ?? string.Empty).Trim();
    //        if (TryParseDouble(text, out var val))
    //        {
    //            Value = val; // DP 内部会 Coerce
    //        }
    //    }

    //    // 无论提交/取消，都用当前 Value 刷新显示文本
    //    SyncDisplayText();
    //    IsEditable = false;
    //    Keyboard.ClearFocus();
    //}

    private void FinishEdit(bool commit)
    {
        if (!IsEditable)
        {
            return;
        }

        if (commit)
        {
            // 先强行清除焦点，确保中文输入法 (IME) 等悬停文本被完全提交到 Text 属性
            Keyboard.ClearFocus();

            var text = (EditBox.Text ?? string.Empty).Trim();
            if (TryParseDouble(text, out var val))
            {
                // 🌟 终极防弹操作 1：使用 SetCurrentValue 赋值，绝对不破坏 XAML 里的 TwoWay 绑定体系
                SetCurrentValue(ValueProperty, val);

                // 🌟 终极防弹操作 2：强行揪出这根绑定的神经管，命令它在 UI 坍塌前，立刻把数据推给 ViewModel！
                var bindingExpression = GetBindingExpression(ValueProperty);
                bindingExpression?.UpdateSource();
            }
        }

        // 无论提交/取消，都用当前 Value 刷新显示文本
        SyncDisplayText();

        // 确认数据已经安全送到大管家手里了，再安全地折叠隐藏输入框
        IsEditable = false;
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

    private void HookWindowClick()
    {
        _parentWindow ??= Window.GetWindow(this);
        if (_parentWindow != null)
        {
            _parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
            _parentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
        }
    }

    private void UnhookWindowClick()
    {
        if (_parentWindow != null)
        {
            _parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
        }
        _parentWindow = null;
    }

    // 全局点击拦截：点击编辑框外部 -> 提交并退出
    private void ParentWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditable)
        {
            return;
        }

        var clicked = e.OriginalSource as DependencyObject;
        if (clicked != null && IsDescendantOf(clicked, EditBox))
        {
            return;
        }

        FinishEdit(commit: true);
    }

    private static bool IsDescendantOf(DependencyObject descendant, DependencyObject ancestor)
    {
        var current = descendant;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        // 先用当前文化（中文环境可能用逗号），再 fallback 到 Invariant
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

    [DllImport("User32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("User32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // 虚拟屏幕（多显示器）物理像素边界
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private static POINT GetCursorPosPixels()
    {
        if (GetCursorPos(out var pt))
        {
            return pt;
        }
        return default;
    }

    private static RECT GetVirtualScreenBoundsPixels()
    {
        var left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height
        };
    }
}

