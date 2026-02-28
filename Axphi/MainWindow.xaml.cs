using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Utils;
using NAudio.Wave;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using System.ComponentModel;


namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SaveFileDialog? _saveChartDialog;
    private OpenFileDialog? _importMusicDialog;

    //private MediaFoundationReader? _musicReader;
    //private WasapiOut? _wasapiOut;

    //private DispatcherTimer? _dispatcherTimer;
    //private Stopwatch? _renderStopwatch;


    private readonly MainViewModel _mainViewModel;


    public MainWindow(
        MainViewModel mainViewModel)
    {
        
        
        InitializeComponent();

        _mainViewModel = mainViewModel;
        DataContext = mainViewModel;

        // 初始化：让下面数值与画布控制点保持一致
        mainViewModel.BezierViewModel.X1 = _p1.X;
        mainViewModel.BezierViewModel.Y1 = _p1.Y;
        mainViewModel.BezierViewModel.X2 = _p2.X;
        mainViewModel.BezierViewModel.Y2 = _p2.Y;


        mainViewModel.BezierViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateVisuals();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BezierViewModel.X1) or nameof(BezierViewModel.Y1) or nameof(BezierViewModel.X2) or nameof(BezierViewModel.Y2))
        {
            _p1 = new Point(_mainViewModel.BezierViewModel.X1, _mainViewModel.BezierViewModel.Y1);
            _p2 = new Point(_mainViewModel.BezierViewModel.X2, _mainViewModel.BezierViewModel.Y2);
            UpdateVisuals();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(31, 31, 31);
        base.OnSourceInitialized(e);
    }

    

    

    [RelayCommand]
    private void LoadDemoChart()
    {
        _mainViewModel.ProjectManager.EditingProject = new Project()
        {
            Chart = DebuggingUtils.CreateDemoChart()
        };
        _mainViewModel.ProjectManager.EditingProjectFilePath = null;
    }

    [RelayCommand]
    private void ImportMusic()
    {
        
        

        _importMusicDialog ??= new OpenFileDialog()
        {
            Title = "Import music",
            Filter = "Audio file|*.mp3;*.ogg;*.wav|Any|*.*",
            CheckFileExists = true,
        };

        if (_importMusicDialog.ShowDialog(this) != true)
        {
            return;
        }

        //ProjectManager.EditingProject.EncodedAudio = System.IO.File.ReadAllBytes(_importMusicDialog.FileName);
        //_musicReader = new MediaFoundationReader(_importMusicDialog.FileName);
        //_wasapiOut ??= new WasapiOut();
        //_wasapiOut.Init(_musicReader);

        // 1. 业务数据逻辑：保存到 Project 对象 (保持不变)
        _mainViewModel.ProjectManager.EditingProject.EncodedAudio = System.IO.File.ReadAllBytes(_importMusicDialog.FileName);

        // 2. 播放器逻辑：直接调用控件的方法 (新逻辑)
        // MainChartDisplay 是你在 XAML 里给控件起的名字
        MainChartDisplay.LoadAudio(_importMusicDialog.FileName);
    }

    [RelayCommand]
    private void SaveChart()
    {
        if (_mainViewModel.ProjectManager.EditingProject is null)
        {
            return;
        }

        if (_mainViewModel.ProjectManager.EditingProjectFilePath is null)
        {
            _saveChartDialog ??= new SaveFileDialog()
            {
                Title = "Save Chart",
                FileName = "New Axphi Project",
                Filter = "Axphi Project|*.axp|Any File|*.*",
                CheckPathExists = true,
            };

            if (_saveChartDialog.ShowDialog(this) != true)
            {
                return;
            }

            _mainViewModel.ProjectManager.EditingProjectFilePath = _saveChartDialog.FileName;
        }

        _mainViewModel.ProjectManager.SaveEditingProject(_mainViewModel.ProjectManager.EditingProjectFilePath);
    }

    [RelayCommand]
    private void MinimizeSelf()
        => WindowState = WindowState.Minimized;

    [RelayCommand]
    private void MaximizeRestoreSelf() => WindowState = WindowState switch
    {
        WindowState.Maximized => WindowState.Normal,
        _ => WindowState.Maximized
    };

    [RelayCommand]
    private void CloseSelf()
        => Close();




    
    //wtfbro我开始瞎写了
    private Point _p1 = new Point(0.75, 0.25);
    private Point _p2 = new Point(0.25, 0.75);



    //测试用数据
    //private Point _p1 = new Point(10.1, 20.2);
    //private Point _p2 = new Point(30.3, 40.4);

    private void UpdateVisuals()
    {
        double w = GraphCanvas.Width;
        double h = GraphCanvas.Height;

        // --- 1. 计算 P1 的屏幕坐标 ---
        // X轴：直接乘宽度
        double x1 = _p1.X * w;
        // Y轴：因为屏幕Y向下增加，所以要用高度减去 (翻转Y轴)
        double y1 = h - (_p1.Y * h);

        // --- 2. 计算 P2 的屏幕坐标 ---
        double x2 = _p2.X * w;
        double y2 = h - (_p2.Y * h);

        // --- 3. 移动蓝色圆点 (Thumb) ---
        // 为了让圆心对准坐标，需要减去圆自身宽度的一半 (7像素)
        Canvas.SetLeft(Thumb1, x1 - Thumb1.Width / 2);
        Canvas.SetTop(Thumb1, y1 - Thumb1.Height / 2);

        Canvas.SetLeft(Thumb2, x2 - Thumb2.Width / 2);
        Canvas.SetTop(Thumb2, y2 - Thumb2.Height / 2);

        // --- 4. 画辅助虚线 ---
        // Line1: 从左下角 (0,0) -> P1
        Line1.X1 = 0;           // 左下角 X=0
        Line1.Y1 = h;           // 左下角 Y=Height
        Line1.X2 = x1;
        Line1.Y2 = y1;

        // Line2: 从右上角 (1,1) -> P2
        Line2.X1 = w;           // 右上角 X=Width
        Line2.Y1 = 0;           // 右上角 Y=0
        Line2.X2 = x2;
        Line2.Y2 = y2;

        // --- 5. 画红色贝塞尔曲线 ---
        UpdateBezierCurve(x1, y1, x2, y2);

        // 【修改】直接给自定义控件赋值
        // 这里为了防止循环触发(改字->重画->改字)，可以加个判断
        // 或者因为我们控件内部逻辑是 TextBlock 显示时才更新，通常没问题

        //string newText = $"{_p1.X:F2}, {_p1.Y:F2}, {_p2.X:F2}, {_p2.Y:F2}";

        // 【核心修复】强制使用不依赖语言环境的格式化
        // 这保证了结果永远是 "0.75, 0.25" 而不是 "0,75, 0,25"
        string newText = string.Format(CultureInfo.InvariantCulture,
            "{0:F2}, {1:F2}, {2:F2}, {3:F2}",
            _p1.X, _p1.Y, _p2.X, _p2.Y);
        //if (BezierValueBox.Text != newText)
        //{
        //    BezierValueBox.Text = newText;
        //}
    }

    // 单独把画曲线提出来，比较清晰
    private void UpdateBezierCurve(double x1, double y1, double x2, double y2)
    {
        double w = GraphCanvas.Width;
        double h = GraphCanvas.Height;

        // 创建贝塞尔几何图形
        // 起点：左下角 (0, h)
        // 终点：右上角 (w, 0)
        // 控制点1：(x1, y1)
        // 控制点2：(x2, y2)

        PathGeometry geometry = new PathGeometry();
        PathFigure figure = new PathFigure();

        // 设置起点 (左下角)
        figure.StartPoint = new Point(0, h);

        // 贝塞尔片段
        BezierSegment segment = new BezierSegment(
            new Point(x1, y1),  // 控制点1
            new Point(x2, y2),  // 控制点2
            new Point(w, 0),    // 终点 (右上角)
            true
        );

        figure.Segments.Add(segment);
        geometry.Figures.Add(figure);

        // 赋值给 XAML 里的 Path
        CurvePath.Data = geometry;
    }


    // 记录当前正在拖拽的那个圆点 (可能是 Thumb1，也可能是 Thumb2)
    private FrameworkElement? _draggingThumb;

    private void GraphCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 点击图的时候：按距离选最近的点，并立即吸附到鼠标
        var mousePos = e.GetPosition(GraphCanvas);
        _draggingThumb = GetNearestThumb(mousePos);
        GraphCanvas.CaptureMouse();
        ApplyMouseToDraggingThumb(mousePos);
        e.Handled = true;
    }

    private void GraphCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingThumb is null || !GraphCanvas.IsMouseCaptured)
        {
            return;
        }

        var mousePos = e.GetPosition(GraphCanvas);
        ApplyMouseToDraggingThumb(mousePos);
        e.Handled = true;
    }

    private void GraphCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (GraphCanvas.IsMouseCaptured)
        {
            GraphCanvas.ReleaseMouseCapture();
        }

        _draggingThumb = null;
        e.Handled = true;
    }

    private FrameworkElement GetNearestThumb(Point mousePos)
    {
        double w = GraphCanvas.Width;
        double h = GraphCanvas.Height;

        // 归一化点 -> 像素点（注意 y 轴翻转）
        var p1 = new Point(_p1.X * w, h - (_p1.Y * h));
        var p2 = new Point(_p2.X * w, h - (_p2.Y * h));

        double d1 = (mousePos.X - p1.X) * (mousePos.X - p1.X) + (mousePos.Y - p1.Y) * (mousePos.Y - p1.Y);
        double d2 = (mousePos.X - p2.X) * (mousePos.X - p2.X) + (mousePos.Y - p2.Y) * (mousePos.Y - p2.Y);
        return d1 <= d2 ? Thumb1 : Thumb2;
    }

    private void ApplyMouseToDraggingThumb(Point mousePos)
    {
        if (_draggingThumb is null)
        {
            return;
        }

        double w = GraphCanvas.Width;
        double h = GraphCanvas.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        double xNorm = mousePos.X / w;
        double yNorm = (h - mousePos.Y) / h;

        // 更新到 ViewModel（X 会在 ViewModel 内部钳制到 0..1）
        if (_draggingThumb == Thumb1)
        {
            _mainViewModel.BezierViewModel.X1 = xNorm;
            _mainViewModel.BezierViewModel.Y1 = yNorm;
        }
        else
        {
            _mainViewModel.BezierViewModel.X2 = xNorm;
            _mainViewModel.BezierViewModel.Y2 = yNorm;
        }
    }

    // 1. 鼠标按下：开始拖拽
    private void Thumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 仍然兼容“直接点圆点拖动”，但实际拖拽逻辑由 Canvas Preview 统一接管
        _draggingThumb = sender as FrameworkElement;
        if (_draggingThumb != null)
        {
            GraphCanvas.CaptureMouse();
            ApplyMouseToDraggingThumb(e.GetPosition(GraphCanvas));
            e.Handled = true;
        }
    }

    // 2. 鼠标抬起：结束拖拽
    private void Thumb_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (GraphCanvas.IsMouseCaptured)
        {
            GraphCanvas.ReleaseMouseCapture();
        }

        _draggingThumb = null;
        e.Handled = true;
    }

    // 3. 鼠标移动：计算坐标并更新
    private void Thumb_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingThumb is null || !GraphCanvas.IsMouseCaptured)
        {
            return;
        }

        ApplyMouseToDraggingThumb(e.GetPosition(GraphCanvas));
        e.Handled = true;
    }
    
    // Helper 方法：解析字符串并更新 _p1, _p2
    private void ApplyInputValues(string inputText)
    {
        // 1. 清理并分割字符串 (移除空格，按逗号分割)
        string[] parts = inputText.Replace(" ", "").Split(',');

        if (parts.Length == 4 &&
            double.TryParse(parts[0], out double p1x) &&
            double.TryParse(parts[1], out double p1y) &&
            double.TryParse(parts[2], out double p2x) &&
            double.TryParse(parts[3], out double p2y))
        {
            // 2. 限制 X 范围 (0~1)
            p1x = Math.Max(0, Math.Min(1, p1x));
            p2x = Math.Max(0, Math.Min(1, p2x));

            // 3. 更新核心数据
            _p1 = new Point(p1x, p1y);
            _p2 = new Point(p2x, p2y);
        }
        // 提示：如果输入错误，这里应该有一个机制来通知用户，但为了简单我们暂时忽略。
    }

    // 【新增】当自定义控件里的值被修改并提交后，触发这个
    private void BezierValueBox_OnValueChanged(object sender, EventArgs e)
    {
        // 1. 获取新文本
        //string inputText = BezierValueBox.Text;

        // 2. 调用你原来的解析逻辑 (ApplyInputValues 代码保留着)
        //ApplyInputValues(inputText);

        // 3. 刷新画面
        UpdateVisuals();
    }


    //private void BezierValueBox_OnValueConstraining(object sender, Axphi.Components.ValueConstrainingEventArgs e)
    //{
    //    // e.Index 表示当前拖动的是第几个数字 (从0开始)
    //    // 格式: P1.X(0), P1.Y(1), P2.X(2), P2.Y(3)

    //    // 规则：只有偶数索引 (0, 2) 是 X轴，需要限制在 0~1
    //    // 奇数索引 (1, 3) 是 Y轴，不做限制
    //    if (e.Index == 0 || e.Index == 2)
    //    {
    //        // 如果计算出的值小于0，强行改成0
    //        if (e.ProposedValue < 0) e.FinalValue = 0;
    //        // 如果计算出的值大于1，强行改成1
    //        else if (e.ProposedValue > 1) e.FinalValue = 1;

    //        // 否则保持原样
    //    }

    //    // 可以在这里加其他规则，比如 Y 轴虽然不限制 0~1，但不能超过 -10~10
    //    // if (e.Index == 1 || e.Index == 3) { ... }
    //}




    // 【辅助方法】判断 child 是否是 parent 的子元素 (或者就是 parent 本身)
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