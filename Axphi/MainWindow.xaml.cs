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


namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private SaveFileDialog? _saveChartDialog;
    private OpenFileDialog? _importMusicDialog;

    private MediaFoundationReader? _musicReader;
    private WasapiOut? _wasapiOut;

    private DispatcherTimer? _dispatcherTimer;
    private Stopwatch? _renderStopwatch;

    public MainViewModel ViewModel { get; }
    public ProjectManager ProjectManager { get; }

    public MainWindow(
        MainViewModel viewModel,
        ProjectManager projectManager)
    {
        ViewModel = viewModel;
        ProjectManager = projectManager;
        DataContext = this;
        InitializeComponent();
        UpdateVisuals();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromRgb(31, 31, 31);
        base.OnSourceInitialized(e);
    }

    [RelayCommand]
    private void PlayPauseChartRendering()
    {
        _renderStopwatch ??= new Stopwatch();
        if (_dispatcherTimer is null)
        {
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Render, RenderTimerCallback, Dispatcher);
        }
        else
        {
            _dispatcherTimer.IsEnabled ^= true;
        }

        if (_dispatcherTimer.IsEnabled)
        {
            _wasapiOut?.Play();
            _renderStopwatch.Start();
        }
        else
        {
            _wasapiOut?.Pause();
            _renderStopwatch.Stop();
        }
    }

    [RelayCommand]
    private void StopChartRendering()
    {
        _dispatcherTimer?.Stop();
        _renderStopwatch?.Stop();
        _renderStopwatch?.Reset();
        _wasapiOut?.Stop();

        chartRenderer.Time = default;
    }

    [RelayCommand]
    private void LoadDemoChart()
    {
        ProjectManager.EditingProject = new Project()
        {
            Chart = DebuggingUtils.CreateDemoChart()
        };
        ProjectManager.EditingProjectFilePath = null;
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

        ProjectManager.EditingProject.EncodedAudio = System.IO.File.ReadAllBytes(_importMusicDialog.FileName);
        _musicReader = new MediaFoundationReader(_importMusicDialog.FileName);
        _wasapiOut ??= new WasapiOut();
        _wasapiOut.Init(_musicReader);
    }

    [RelayCommand]
    private void SaveChart()
    {
        if (ProjectManager.EditingProject is null)
        {
            return;
        }

        if (ProjectManager.EditingProjectFilePath is null)
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

            ProjectManager.EditingProjectFilePath = _saveChartDialog.FileName;
        }

        ProjectManager.SaveEditingProject(ProjectManager.EditingProjectFilePath);
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

    private void RenderTimerCallback(object? sender, EventArgs e)
    {
        if (_wasapiOut is not null &&
            _wasapiOut.PlaybackState == PlaybackState.Playing)
        {
            chartRenderer.Time = _wasapiOut.GetPositionTimeSpan();
            return;
        }

        _renderStopwatch ??= new Stopwatch();
        chartRenderer.Time = _renderStopwatch.Elapsed;
    }
    //wtfbro我开始瞎写了
    private Point _p1 = new Point(0.75, 0.25);
    private Point _p2 = new Point(0.25, 0.75);

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
        string newText = $"{_p1.X:F2}, {_p1.Y:F2}, {_p2.X:F2}, {_p2.Y:F2}";
        if (BezierValueBox.Text != newText)
        {
            BezierValueBox.Text = newText;
        }
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
    private FrameworkElement _draggingThumb = null;
    // 1. 鼠标按下：开始拖拽
    private void Thumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 获取被点击的圆点
        _draggingThumb = sender as FrameworkElement;

        // 【重要】捕获鼠标
        // 这样即使鼠标移出了 Canvas 范围，程序依然能收到移动事件，防止拖断
        if (_draggingThumb != null)
        {
            _draggingThumb.CaptureMouse();
        }
    }

    // 2. 鼠标抬起：结束拖拽
    private void Thumb_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingThumb != null)
        {
            // 释放鼠标捕获
            _draggingThumb.ReleaseMouseCapture();
            _draggingThumb = null;
        }
    }

    // 3. 鼠标移动：计算坐标并更新
    private void Thumb_MouseMove(object sender, MouseEventArgs e)
    {
        // 如果没有在拖拽，直接忽略
        if (_draggingThumb == null) return;

        // 获取鼠标在 Canvas 上的像素坐标
        Point mousePos = e.GetPosition(GraphCanvas);
        double w = GraphCanvas.Width;
        double h = GraphCanvas.Height;

        // --- 核心算法：像素坐标 -> 归一化坐标 (0~1) ---

        // 1. 计算 X (范围 0~1)
        double xNorm = mousePos.X / w;

        // 2. 计算 Y (范围 0~1)
        // 屏幕Y向下增加，数学Y向上增加，所以要反过来算：(总高 - 鼠标Y) / 总高
        double yNorm = (h - mousePos.Y) / h;

        // --- 实施约束：X 值不允许小于 0 或大于 1 ---
        if (xNorm < 0) xNorm = 0;
        if (xNorm > 1) xNorm = 1;

        // --- 更新数据 ---
        // 判断当前拖的是哪个点，更新对应的 _p1 或 _p2
        if (_draggingThumb == Thumb1)
        {
            _p1 = new Point(xNorm, yNorm);
        }
        else if (_draggingThumb == Thumb2)
        {
            _p2 = new Point(xNorm, yNorm);
        }

        // --- 刷新画面 ---
        // 数据变了，重新画图
        UpdateVisuals();
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
        string inputText = BezierValueBox.Text;

        // 2. 调用你原来的解析逻辑 (ApplyInputValues 代码保留着)
        ApplyInputValues(inputText);

        // 3. 刷新画面
        UpdateVisuals();
    }





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