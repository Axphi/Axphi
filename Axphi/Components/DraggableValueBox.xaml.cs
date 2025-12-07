using Axphi.Utilities;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.Components;

// 1. 数据块定义：我们要把字符串切成这样的小块
// 1. 数据块定义：实现 INotifyPropertyChanged
public partial class ValueChunk : ObservableObject
{
    [ObservableProperty]
    private string _displayText = "";

    [ObservableProperty]
    private double _value;

    // 【新增】标记这是第几个数字 (0, 1, 2, 3...)
    public int NumberIndex { get; set; } = -1;

    public bool IsNumber { get; set; }
    }
}

// 2. 模板选择器 (XAML 用)
public class ChunkTemplateSelector : DataTemplateSelector
{
    public DataTemplate NumberTemplate { get; set; }
    public DataTemplate TextTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        var chunk = item as ValueChunk;
        return chunk.IsNumber ? NumberTemplate : TextTemplate;
    }
}


public class ValueConstrainingEventArgs : EventArgs
{
    public int Index { get; }
    public double ProposedValue { get; set; } // 控件算出来的值
    public double FinalValue { get; set; }    // 外部修改后的最终值

    public ValueConstrainingEventArgs(int index, double value)
    {
        Index = index;
        ProposedValue = value;
        FinalValue = value;
    }
}

public partial class DraggableValueBox : UserControl
{
    public event EventHandler? ValueChanged;

    // 内部数据源
    private ObservableCollection<ValueChunk> _chunks = new();

    // 拖拽状态记录
    private bool _isDragging = false;
    private Point _startDragScreenPos;
    private double _startDragValue;
    private ValueChunk? _activeChunk;
    private FrameworkElement? _activeElement;
    private bool _hasMovedSignificantly = false; // 区分是点击还是拖拽

    // 【新增】数值限制事件
    public event EventHandler<ValueConstrainingEventArgs>? ValueConstraining;

    // 记录当前正在编辑的 TextBox，方便我们从全局事件里找到它
    private TextBox? _currentEditingTextBox = null;

    public DraggableValueBox()
    {
        InitializeComponent();
        MainItemsControl.ItemsSource = _chunks;
    }

    #region Dependency Properties

    // 1. Text: 完整的字符串 "0.25, 0.5"
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(DraggableValueBox),
            new PropertyMetadata("", OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 2. DragSpeed: 拖拽速度 (默认 0.01)
    public static readonly DependencyProperty DragSpeedProperty =
        DependencyProperty.Register("DragSpeed", typeof(double), typeof(DraggableValueBox), new PropertyMetadata(0.01));

    public double DragSpeed
    {
        get => (double)GetValue(DragSpeedProperty);
        set => SetValue(DragSpeedProperty, value);
    }

    // 3. Format: 数字格式 (默认 F2)
    public static readonly DependencyProperty NumberFormatProperty =
        DependencyProperty.Register("NumberFormat", typeof(string), typeof(DraggableValueBox), new PropertyMetadata("F2"));

    public string NumberFormat
    {
        get => (string)GetValue(NumberFormatProperty);
        set => SetValue(NumberFormatProperty, value);
    }

    // 4. Minimum: 最小值 (默认负无穷)
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register("Minimum", typeof(double), typeof(DraggableValueBox), new PropertyMetadata(double.MinValue));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    // 5. Maximum: 最大值 (默认正无穷)
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register("Maximum", typeof(double), typeof(DraggableValueBox), new PropertyMetadata(double.MaxValue));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    #endregion

    // 当外部 Text 改变时，解析它
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as DraggableValueBox;
        if (control == null) return;

        // 如果正在拖拽中，不要重新解析，否则会打断操作
        if (control._isDragging) return;

        control.ParseText((string)e.NewValue);
    }

    // --- 核心解析逻辑：正则拆分数字和文本 ---
    private void ParseText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        _chunks.Clear();

        // 正则：匹配所有浮点数 (包括负数)
        string pattern = @"-?\d+(\.\d+)?";
        var matches = Regex.Matches(text, pattern);

        int lastIndex = 0;
        int numberCounter = 0; // 【新增】数字计数器

        foreach (Match match in matches)
        {
            // 1. 添加数字前面的普通文本 (例如逗号)
            if (match.Index > lastIndex)
            {
                _chunks.Add(new ValueChunk
                {
                    DisplayText = text.Substring(lastIndex, match.Index - lastIndex),
                    IsNumber = false
                });
            }

            // 2. 添加数字本身
            if (double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                _chunks.Add(new ValueChunk
                {
                    DisplayText = val.ToString(NumberFormat, CultureInfo.InvariantCulture), // 这里也要改
                    Value = val,
                    IsNumber = true,
                    NumberIndex = numberCounter++ // 设置数字索引
                });
            }

            lastIndex = match.Index + match.Length;
        }
        if (lastIndex < text.Length)
        {
            _chunks.Add(new ValueChunk
            {
                DisplayText = text.Substring(lastIndex),
                IsNumber = false
            });
        }


        // 3. 添加剩余的文本
        if (lastIndex < text.Length)
        {
            _chunks.Add(new ValueChunk
            {
                DisplayText = text.Substring(lastIndex),
                IsNumber = false
            });
        }
    }

    // --- 交互逻辑：鼠标按下 ---
    private void Number_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var grid = sender as Grid;
        var chunk = grid?.Tag as ValueChunk;
        if (chunk == null) return;

        _activeElement = grid;
        _activeChunk = chunk;
        _startDragScreenPos = CursorUtils.GetPosition(); // 记录屏幕绝对坐标
        _startDragValue = chunk.Value;
        _isDragging = true;
        _hasMovedSignificantly = false;

        // 隐藏鼠标，锁定捕获
        //Mouse.OverrideCursor = Cursors.None;


        Mouse.OverrideCursor = Cursors.SizeWE; // 双向箭头 <->

        _activeElement.CaptureMouse();
    }

    // --- 交互逻辑：鼠标拖动 (核心) ---
    private void Number_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _activeChunk == null) return;

        Point currentScreenPos = CursorUtils.GetPosition();
        double deltaX = currentScreenPos.X - _startDragScreenPos.X;

        // 如果移动距离太小，视为静止，防抖动
        if (Math.Abs(deltaX) < 2 && !_hasMovedSignificantly) return;

        _hasMovedSignificantly = true;

        // 1. 计算倍率
        double speed = DragSpeed;

        // Shift: 微调 (速度 * 0.1)
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            speed *= 0.1;

        // Ctrl: 吸附 (这里实现为加速 * 10，或者你可以改成取整逻辑)
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            speed *= 10.0;

        // 2. 计算新值
        double newValue = _startDragValue + (deltaX * speed);


        // 【新增代码】: 实施数值钳制 (Clamp)
        // 如果小于 Min，就死死定在 Min；如果大于 Max，就死死定在 Max
        if (newValue < Minimum) newValue = Minimum;
        if (newValue > Maximum) newValue = Maximum;

        // 3. 【核心新增】询问外部：你要对这个特定的数字做特殊限制吗？
        if (ValueConstraining != null)
        {
            var args = new ValueConstrainingEventArgs(_activeChunk.NumberIndex, newValue);
            ValueConstraining.Invoke(this, args);
            newValue = args.FinalValue; // 使用外部修正后的值
        }


        // Ctrl 吸附逻辑 (可选：吸附到整数)
        // if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        //    newValue = Math.Round(newValue);

        // 3. 更新界面
        _activeChunk.Value = newValue;
        // 强制用英文格式显示
        _activeChunk.DisplayText = newValue.ToString(NumberFormat, CultureInfo.InvariantCulture);

        // 强制刷新 UI 显示 (因为 ObservableCollection 有时候不刷属性)
        // 这里简单粗暴一点，重新生成字符串通知外部
        RebuildAndNotify();

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
            CursorUtils.SetPosition((int)newPos.X, (int)newPos.Y);
            // 重要：修正起始点，防止数值跳变
            _startDragScreenPos = new Point(newPos.X - deltaX, _startDragScreenPos.Y);
        }
    }

    // --- 交互逻辑：鼠标抬起 ---
    private void Number_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        _activeElement.ReleaseMouseCapture();
        Mouse.OverrideCursor = null; // 恢复光标

        // 如果几乎没动，说明是【点击】，进入编辑模式
        if (!_hasMovedSignificantly && _activeChunk != null)
        {
            EnterEditMode(_activeElement);
        }
        else
        {
            // 否则是【拖拽结束】，再次确认提交
            RebuildAndNotify();
        }

        _activeChunk = null;
        _activeElement = null;
    }

    // --- 编辑模式逻辑 ---
    private void EnterEditMode(FrameworkElement container)
    {
        // 找到里面的 TextBox 和 TextBlock
        var textBox = FindVisualChild<TextBox>(container);
        var border = FindVisualChild<Border>(container);

        if (textBox != null && border != null)
        {
            border.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
            textBox.Focus();
            textBox.SelectAll();

            // 【新增】记录当前编辑框，并开始监听窗口全局点击
            _currentEditingTextBox = textBox;
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
            }
        }
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // 提交修改
            var textBox = sender as TextBox;
            FinishEdit(textBox);
        }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        FinishEdit(sender as TextBox);
    }

    private void FinishEdit(TextBox textBox)
    {
        if (textBox == null) return;

        // 1. 尝试解析输入的值
        if (double.TryParse(textBox.Text, out double val))
        {
            // 更新 Chunk 数据
            // 注意：要找回这个 TextBox 对应的 Chunk 有点麻烦，
            // 最好的办法是重新解析整个 Text 流程，但这里我们简化处理：
            // 直接触发 Text 更新，让 OnTextChanged 重新解析一切

            // 简单做法：我们知道当前的 _activeChunk 没变，或者通过 DataContext 拿
            var chunk = textBox.DataContext as ValueChunk;
            if (chunk != null)
            {
                chunk.Value = val;
                chunk.DisplayText = val.ToString(NumberFormat);
            }
        }

        // 2. 恢复 UI
        var parent = VisualTreeHelper.GetParent(textBox) as Grid;
        if (parent != null)
        {
            var border = FindVisualChild<Border>(parent);
            if (border != null) border.Visibility = Visibility.Visible;
            textBox.Visibility = Visibility.Collapsed;
        }


        // 【新增】编辑结束，移除窗口监听，清空引用
        var parentWindow = Window.GetWindow(this);
        if (parentWindow != null)
        {
            parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
        }
        _currentEditingTextBox = null;

        
        // 3. 通知外部
        RebuildAndNotify();
    }

    // 【新增】全局点击拦截
    private void ParentWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 如果当前没在编辑，或者找不到编辑框，直接退出
        if (_currentEditingTextBox == null) return;

        var clickedElement = e.OriginalSource as DependencyObject;

        // 1. 判断点击的是不是当前输入框（或其内部）
        // 如果是点在输入框上（比如选文本），就啥也不做，让它继续
        if (IsChildOf(_currentEditingTextBox, clickedElement))
        {
            return;
        }

        // 2. 如果点的是外面 -> 强制提交！
        FinishEdit(_currentEditingTextBox);
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

    // --- 辅助：重组字符串并通知 ---
    private void RebuildAndNotify()
    {
        // 把所有的 Chunk 拼回成字符串
        string fullText = string.Join("", _chunks.Select(c => c.DisplayText));

        // 更新依赖属性 (这将触发 TwoWay Binding 更新 ViewModel)
        Text = fullText;

        // 触发事件
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    // 辅助查找子控件
    private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    // TextBox 加载时自动聚焦 (可选)
    private void EditBox_Loaded(object sender, RoutedEventArgs e)
    {
        var tb = sender as TextBox;
        if (tb.Visibility == Visibility.Visible)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

}

