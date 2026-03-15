using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace Axphi.Components;

public class StringList : List<string>, IAddChild
{
    // 添加子对象（XAML 中的子元素，例如 <sys:String>）
    public void AddChild(object value)
    {
        // 可根据需要添加对其他类型的处理，或抛出异常
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            Add(str.Trim());
        }
    }
    // 处理 <comp:StringList>tap, drag, hold</comp:StringList> 这种纯文本写法
    public void AddText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // 自动按逗号、换行或空白字符分割，并过滤掉空项
        var items = text.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                Add(trimmed);
            }
        }
    }
    
}
public partial class DraggableOptionBox : UserControl
{
    public event EventHandler? ValueChanged;

    private Point _lastDragScreenPos;
    
    private double _dragDeltaAccumulator;
    private double _totalDragDistance; // 区分点击和拖动的阈值记录

    #region Dependency Properties


    // 在 DraggableOptionBox 类中添加
    private static readonly DependencyPropertyKey IsDraggingPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsDragging),
            typeof(bool),
            typeof(DraggableOptionBox),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsDraggingProperty =
        IsDraggingPropertyKey.DependencyProperty;

    public bool IsDragging
    {
        get => (bool)GetValue(IsDraggingProperty);
        private set => SetValue(IsDraggingPropertyKey, value);
    }


    // 1. 当前选中的字符串值
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(DraggableOptionBox),
        new FrameworkPropertyMetadata("Tap", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    
    // 2. 预设选项列表
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options),
        typeof(StringList),
        typeof(DraggableOptionBox),
        new PropertyMetadata(null, OnOptionsChanged));

    public StringList Options
    {
        get => (StringList)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    // 3. 拖拽灵敏度 (默认拖动 30 像素切换一个选项)
    public static readonly DependencyProperty DragSensitivityProperty = DependencyProperty.Register(
        nameof(DragSensitivity),
        typeof(double),
        typeof(DraggableOptionBox),
        new PropertyMetadata(30.0));

    public double DragSensitivity
    {
        get => (double)GetValue(DragSensitivityProperty);
        set => SetValue(DragSensitivityProperty, value);
    }

    #endregion

    public DraggableOptionBox()
    {
        InitializeComponent();

        // ❌ 删除这三行旧代码（去掉了 handledEventsToo = true 的逻辑）
        // Container.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseDown), true);
        // Container.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseUp), true);
        // Container.AddHandler(MouseMoveEvent, new MouseEventHandler(OnMouseMove), true);

        // ✅ 替换为常规的事件订阅：
        Container.MouseLeftButtonDown += OnMouseDown;
        Container.MouseLeftButtonUp += OnMouseUp;
        Container.MouseMove += OnMouseMove;

        // 像 DraggableValueBox 一样，在 Loaded 时处理初始化逻辑
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(Value))
            {
                var list = Options?.ToList();
                if (list != null && list.Count > 0)
                {
                    Value = list[0];
                }
            }
        };
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DraggableOptionBox obj)
        {
            obj.ValueChanged?.Invoke(obj, EventArgs.Empty);
        }
    }

    private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DraggableOptionBox obj && string.IsNullOrEmpty(obj.Value))
        {
            // 如果刚绑定了列表，且当前 Value 为空，自动选中第一个
            var list = obj.Options?.ToList();
            if (list != null && list.Any())
            {
                obj.Value = list[0];
            }
        }
    }

    // --- 交互逻辑：鼠标按下 ---
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        IsDragging = true;

        if (OptionsPopup.IsOpen)
        {
            OptionsPopup.IsOpen = false;
        }

        var cursor = GetCursorPosPixels();
        _lastDragScreenPos = new Point(cursor.X, cursor.Y);

        IsDragging = true;
        _dragDeltaAccumulator = 0;
        _totalDragDistance = 0;

        Mouse.OverrideCursor = Cursors.SizeWE;
        Container.CaptureMouse();
        e.Handled = true;
    }

    // --- 交互逻辑：鼠标拖动 ---
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDragging) return;

        var cursor = GetCursorPosPixels();
        Point currentScreenPos = new Point(cursor.X, cursor.Y);

        double deltaX = currentScreenPos.X - _lastDragScreenPos.X;

        var bounds = GetVirtualScreenBoundsPixels();
        var boundsWidth = bounds.Right - bounds.Left;

        // 防屏幕跳闪瞬间的巨额 delta
        if (boundsWidth > 0 && Math.Abs(deltaX) > boundsWidth * 0.5)
        {
            _lastDragScreenPos = currentScreenPos;
            return;
        }

        // 记录总移动距离以区分点击和拖拽
        _totalDragDistance += Math.Abs(deltaX);

        // 如果真正处于拖拽状态 (防手抖判定阈值 > 2px)
        if (_totalDragDistance > 2)
        {
            _dragDeltaAccumulator += deltaX;

            // 根据距离判定切换选项
            double sensitivity = DragSensitivity;

            // 可选：Shift 慢速微调，Ctrl 快速切换
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) sensitivity *= 2.0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) sensitivity *= 0.5;

            // 累加值超过了灵敏度，触发切换
            if (Math.Abs(_dragDeltaAccumulator) >= sensitivity)
            {
                int steps = (int)(_dragDeltaAccumulator / sensitivity); // 算出跨越了几个选项
                _dragDeltaAccumulator -= steps * sensitivity; // 扣除消耗掉的像素，保留余数

                ShiftOption(steps);
            }
        }

        _lastDragScreenPos = currentScreenPos;

        // --- 屏幕边缘无限回环 (Infinite Wrap) ---
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
            _lastDragScreenPos = newPos;
        }

        e.Handled = true;
    }

    // --- 交互逻辑：鼠标抬起 ---
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !IsDragging) return;

        IsDragging = false;
        Container.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;

        // 如果从按下到抬起，鼠标基本没动过，就当作是点击事件，弹出列表菜单
        if (_totalDragDistance <= 2)
        {
            OptionsPopup.IsOpen = true;
        }

        e.Handled = true;
    }

    // --- 选项切换核心逻辑 ---
    private void ShiftOption(int steps)
    {
        var list = Options?.ToList();
        if (list == null || list.Count == 0) return;

        int currentIndex = list.IndexOf(Value);
        if (currentIndex < 0) currentIndex = 0; // 容错：如果当前值不在列表里，默认从头开始

        // 计算新索引 (C# 里的 % 对负数取模还是负数，所以要特殊处理 wrap around)
        int newIndex = (currentIndex + steps) % list.Count;
        if (newIndex < 0)
        {
            newIndex += list.Count;
        }

        Value = list[newIndex];
    }

    // --- 菜单按钮点击事件 ---
    private void OptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string newValue)
        {
            Value = newValue;
            OptionsPopup.IsOpen = false; // 选完自动关闭
        }
    }

    #region Win32 API
    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("User32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("User32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private static POINT GetCursorPosPixels()
    {
        if (GetCursorPos(out var pt)) return pt;
        return default;
    }

    private static RECT GetVirtualScreenBoundsPixels()
    {
        var left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new RECT { Left = left, Top = top, Right = left + width, Bottom = top + height };
    }
    #endregion
}