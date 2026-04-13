using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Axphi.Components;

public partial class BezierCurveEditor : UserControl
{
    // 记录正在拖拽的圆点
    private FrameworkElement? _draggingThumb;

    private const int GridDivisions = 8;

    public BezierCurveEditor()
    {
        InitializeComponent();
        this.SizeChanged += (s, e) => UpdateVisuals(); // 窗口大小改变时重绘
    }

    #region 核心：依赖属性 (Dependency Properties)

    // 统一定义一个回调：只要这四个值里有任何一个改变了，就触发重绘
    private static void OnCoordinateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BezierCurveEditor editor)
        {
            editor.UpdateVisuals();
        }
    }

    public static readonly DependencyProperty X1Property = DependencyProperty.Register(
        nameof(X1), typeof(double), typeof(BezierCurveEditor),
        new FrameworkPropertyMetadata(0.75, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCoordinateChanged));

    public static readonly DependencyProperty Y1Property = DependencyProperty.Register(
        nameof(Y1), typeof(double), typeof(BezierCurveEditor),
        new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCoordinateChanged));

    public static readonly DependencyProperty X2Property = DependencyProperty.Register(
        nameof(X2), typeof(double), typeof(BezierCurveEditor),
        new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCoordinateChanged));

    public static readonly DependencyProperty Y2Property = DependencyProperty.Register(
        nameof(Y2), typeof(double), typeof(BezierCurveEditor),
        new FrameworkPropertyMetadata(0.75, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCoordinateChanged));

    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    #endregion

    #region 视觉渲染逻辑

    private void UpdateVisuals()
    {
        double w = GraphCanvas.ActualWidth;
        double h = GraphCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        UpdateGrid(w, h);

        double px1 = X1 * w;
        double py1 = h - (Y1 * h);
        double px2 = X2 * w;
        double py2 = h - (Y2 * h);

        Canvas.SetLeft(Thumb1, px1 - Thumb1.Width / 2);
        Canvas.SetTop(Thumb1, py1 - Thumb1.Height / 2);
        Canvas.SetLeft(Thumb2, px2 - Thumb2.Width / 2);
        Canvas.SetTop(Thumb2, py2 - Thumb2.Height / 2);

        Line1.X1 = 0; Line1.Y1 = h; Line1.X2 = px1; Line1.Y2 = py1;
        Line2.X1 = w; Line2.Y1 = 0; Line2.X2 = px2; Line2.Y2 = py2;

        UpdateBezierCurve(px1, py1, px2, py2);
    }

    private void UpdateBezierCurve(double px1, double py1, double px2, double py2)
    {
        double w = GraphCanvas.ActualWidth;
        double h = GraphCanvas.ActualHeight;

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, h) };
        figure.Segments.Add(new BezierSegment(new Point(px1, py1), new Point(px2, py2), new Point(w, 0), true));
        geometry.Figures.Add(figure);
        CurvePath.Data = geometry;
    }

    private void UpdateGrid(double w, double h)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            for (int i = 0; i <= GridDivisions; i++)
            {
                double x = w * i / GridDivisions;
                ctx.BeginFigure(new Point(x, 0), false, false);
                ctx.LineTo(new Point(x, h), true, false);

                double y = h * i / GridDivisions;
                ctx.BeginFigure(new Point(0, y), false, false);
                ctx.LineTo(new Point(w, y), true, false);
            }
        }

        geometry.Freeze();
        GridPath.Data = geometry;
    }

    #endregion

    #region 鼠标拖拽逻辑

    private void GraphCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var mousePos = e.GetPosition(GraphCanvas);
        _draggingThumb = GetNearestThumb(mousePos);
        GraphCanvas.CaptureMouse();
        ApplyMouseToDraggingThumb(mousePos);
        e.Handled = true;
    }

    private void GraphCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingThumb is null || !GraphCanvas.IsMouseCaptured) return;
        ApplyMouseToDraggingThumb(e.GetPosition(GraphCanvas));
        e.Handled = true;
    }

    private void GraphCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (GraphCanvas.IsMouseCaptured) GraphCanvas.ReleaseMouseCapture();
        _draggingThumb = null;
        e.Handled = true;
    }


    // 这三个函数永远不会被触发
    private void Thumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingThumb = sender as FrameworkElement;
        if (_draggingThumb != null)
        {
            GraphCanvas.CaptureMouse();
            ApplyMouseToDraggingThumb(e.GetPosition(GraphCanvas));
            e.Handled = true;
        }
    }

    private void Thumb_MouseUp(object sender, MouseButtonEventArgs e) => GraphCanvas_PreviewMouseLeftButtonUp(sender, e);
    private void Thumb_MouseMove(object sender, MouseEventArgs e) => GraphCanvas_PreviewMouseMove(sender, e);

    private FrameworkElement GetNearestThumb(Point mousePos)
    {
        double w = GraphCanvas.ActualWidth;
        double h = GraphCanvas.ActualHeight;
        var p1 = new Point(X1 * w, h - (Y1 * h));
        var p2 = new Point(X2 * w, h - (Y2 * h));

        double d1 = (mousePos.X - p1.X) * (mousePos.X - p1.X) + (mousePos.Y - p1.Y) * (mousePos.Y - p1.Y);
        double d2 = (mousePos.X - p2.X) * (mousePos.X - p2.X) + (mousePos.Y - p2.Y) * (mousePos.Y - p2.Y);
        return d1 <= d2 ? Thumb1 : Thumb2;
    }

    private void ApplyMouseToDraggingThumb(Point mousePos)
    {
        if (_draggingThumb is null) return;

        double w = GraphCanvas.ActualWidth;
        double h = GraphCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double xNorm = mousePos.X / w;
        double yNorm = (h - mousePos.Y) / h;

        ModifierKeys modifiers = Keyboard.Modifiers;

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            if (_draggingThumb == Thumb1)
            {
                // 左下角端点(0,0)的手柄只允许在两条轴线上：(0,y) 或 (x,0)
                if (Math.Abs(xNorm) <= Math.Abs(yNorm))
                {
                    xNorm = 0.0;
                }
                else
                {
                    yNorm = 0.0;
                }
            }
            else
            {
                // 右上角端点(1,1)的手柄只允许在两条轴线上：(1,y) 或 (x,1)
                if (Math.Abs(xNorm - 1.0) <= Math.Abs(yNorm - 1.0))
                {
                    xNorm = 1.0;
                }
                else
                {
                    yNorm = 1.0;
                }
            }
        }

        if ((modifiers & ModifierKeys.Control) != 0)
        {
            double step = 1.0 / GridDivisions;
            xNorm = Math.Round(xNorm / step) * step;
            yNorm = Math.Round(yNorm / step) * step;
        }

        // X 仍限制在 0~1，Y 可自由超出范围
        xNorm = Math.Clamp(xNorm, 0.0, 1.0);

        // 直接更新依赖属性，由于我们设置了 BindsTwoWayByDefault，这会自动同步给外界绑定的 ViewModel！
        if (_draggingThumb == Thumb1)
        {
            X1 = xNorm;
            Y1 = yNorm;
        }
        else
        {
            X2 = xNorm;
            Y2 = yNorm;
        }
    }

    #endregion
}