using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Axphi.Components;

public sealed class TimelineRenderSurface : FrameworkElement
{
    private const double KeyFrameSize = 10.0;
    private const double HalfKeyFrameSize = KeyFrameSize / 2.0;

    private static readonly Brush LineFill = new SolidColorBrush(Color.FromRgb(43, 87, 154));
    private static readonly Pen LineStroke = new Pen(new SolidColorBrush(Color.FromRgb(75, 134, 217)), 1.0);
    private static readonly Brush KeyFrameFill = new SolidColorBrush(Color.FromRgb(160, 160, 160));
    private static readonly Pen KeyFrameStroke = new Pen(new SolidColorBrush(Color.FromRgb(209, 209, 209)), 1.0);
    private static readonly Brush KeyFrameHoverFill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Pen KeyFrameHoverStroke = new Pen(new SolidColorBrush(Color.FromRgb(0, 122, 204)), 1.25);
    private static readonly Brush KeyFrameSelectedFill = new SolidColorBrush(Color.FromRgb(0, 122, 204));
    private static readonly Pen KeyFrameSelectedStroke = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 1.25);
    private static readonly Brush KeyFrameDraggingFill = new SolidColorBrush(Color.FromRgb(255, 204, 102));
    private static readonly Pen KeyFrameDraggingStroke = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 1.5);
    private static readonly Brush HitTestFill = Brushes.Transparent;

    private readonly HashSet<IKeyFrame> _selectedKeyFrames = new(ReferenceEqualityComparer.Instance);
    private TimelineKeyFrameRef? _hoveredKeyFrame;
    private TimelineKeyFrameRef? _draggingKeyFrame;
    private Point _dragStartMousePosition;
    private int _dragStartTick;

    static TimelineRenderSurface()
    {
        LineFill.Freeze();
        LineStroke.Freeze();
        KeyFrameFill.Freeze();
        KeyFrameStroke.Freeze();
        KeyFrameHoverFill.Freeze();
        KeyFrameHoverStroke.Freeze();
        KeyFrameSelectedFill.Freeze();
        KeyFrameSelectedStroke.Freeze();
        KeyFrameDraggingFill.Freeze();
        KeyFrameDraggingStroke.Freeze();
    }

    public TimelineRenderSurface()
    {
        Focusable = true;
    }

    public Chart? Chart
    {
        get => (Chart?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    public static readonly DependencyProperty ChartProperty =
        DependencyProperty.Register(
            nameof(Chart),
            typeof(Chart),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnChartChanged));

    public ITrackLayoutProvider? LayoutProvider
    {
        get => (ITrackLayoutProvider?)GetValue(LayoutProviderProperty);
        set => SetValue(LayoutProviderProperty, value);
    }

    public static readonly DependencyProperty LayoutProviderProperty =
        DependencyProperty.Register(
            nameof(LayoutProvider),
            typeof(ITrackLayoutProvider),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Point ViewportLocation
    {
        get => (Point)GetValue(ViewportLocationProperty);
        set => SetValue(ViewportLocationProperty, value);
    }

    public static readonly DependencyProperty ViewportLocationProperty =
        DependencyProperty.Register(
            nameof(ViewportLocation),
            typeof(Point),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(
                default(Point),
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double PixelPerTick
    {
        get => (double)GetValue(PixelPerTickProperty);
        set => SetValue(PixelPerTickProperty, value);
    }

    public static readonly DependencyProperty PixelPerTickProperty =
        DependencyProperty.Register(
            nameof(PixelPerTick),
            typeof(double),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(
                1.0,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double MinViewportX
    {
        get => (double)GetValue(MinViewportXProperty);
        set => SetValue(MinViewportXProperty, value);
    }

    public static readonly DependencyProperty MinViewportXProperty =
        DependencyProperty.Register(nameof(MinViewportX), typeof(double), typeof(TimelineRenderSurface), new PropertyMetadata(-5.0));

    public double MinZoom
    {
        get => (double)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register(nameof(MinZoom), typeof(double), typeof(TimelineRenderSurface), new PropertyMetadata(0.1));

    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(TimelineRenderSurface), new PropertyMetadata(40.0));

    public double ZoomStep
    {
        get => (double)GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    public static readonly DependencyProperty ZoomStepProperty =
        DependencyProperty.Register(nameof(ZoomStep), typeof(double), typeof(TimelineRenderSurface), new PropertyMetadata(0.1));

    public double HorizontalScrollSpeed
    {
        get => (double)GetValue(HorizontalScrollSpeedProperty);
        set => SetValue(HorizontalScrollSpeedProperty, value);
    }

    public static readonly DependencyProperty HorizontalScrollSpeedProperty =
        DependencyProperty.Register(nameof(HorizontalScrollSpeed), typeof(double), typeof(TimelineRenderSurface), new PropertyMetadata(0.1));

    public double VerticalScrollSpeed
    {
        get => (double)GetValue(VerticalScrollSpeedProperty);
        set => SetValue(VerticalScrollSpeedProperty, value);
    }

    public static readonly DependencyProperty VerticalScrollSpeedProperty =
        DependencyProperty.Register(nameof(VerticalScrollSpeed), typeof(double), typeof(TimelineRenderSurface), new PropertyMetadata(0.1));

    public double ContentHeight
    {
        get => (double)GetValue(ContentHeightProperty);
        set => SetValue(ContentHeightProperty, value);
    }

    public static readonly DependencyProperty ContentHeightProperty =
        DependencyProperty.Register(
            nameof(ContentHeight),
            typeof(double),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnScrollableBoundsChanged));

    public int LayoutVersion
    {
        get => (int)GetValue(LayoutVersionProperty);
        set => SetValue(LayoutVersionProperty, value);
    }

    public static readonly DependencyProperty LayoutVersionProperty =
        DependencyProperty.Register(
            nameof(LayoutVersion),
            typeof(int),
            typeof(TimelineRenderSurface),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        CoerceViewportLocation();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(HitTestFill, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (Chart is not { } chart || LayoutProvider is not { } layoutProvider)
        {
            return;
        }

        var viewport = ViewportLocation;
        var viewportRect = new Rect(0, 0, ActualWidth, ActualHeight);
        double pixelsPerTick = PixelPerTick * Zoom;
        double visibleTickStart = Math.Max(0, viewport.X / Math.Max(0.000001, pixelsPerTick));
        double visibleTickEnd = (viewport.X + ActualWidth) / Math.Max(0.000001, pixelsPerTick);

        foreach (var line in chart.JudgementLines)
        {
            DrawLine(drawingContext, line, layoutProvider, viewport, viewportRect, pixelsPerTick);

            if (line.StartTick > visibleTickEnd || line.StartTick + line.DurationTicks < visibleTickStart)
            {
                continue;
            }

            DrawKeyFrames(drawingContext, line, layoutProvider, viewport, viewportRect, visibleTickStart, visibleTickEnd, pixelsPerTick);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        Point position = e.GetPosition(this);
        var hit = HitTestKeyFrame(position);

        if (hit is null)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                _selectedKeyFrames.Clear();
            }

            e.Handled = true;
            InvalidateVisual();
            return;
        }

        bool shouldStartDrag = SelectKeyFrame(hit.Value, toggle: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
        if (shouldStartDrag)
        {
            _draggingKeyFrame = hit;
            _dragStartMousePosition = position;
            _dragStartTick = hit.Value.KeyFrame.Tick;
            CaptureMouse();
        }

        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_draggingKeyFrame is null)
        {
            return;
        }

        _draggingKeyFrame = null;
        ReleaseMouseCapture();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingKeyFrame is not null)
        {
            ProcessDrag(e);
            e.Handled = true;
            return;
        }

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            SetHoveredKeyFrame(null);
            return;
        }

        Point position = e.GetPosition(this);
        if (position.X < 0 || position.Y < 0 || position.X > ActualWidth || position.Y > ActualHeight)
        {
            SetHoveredKeyFrame(null);
            return;
        }

        SetHoveredKeyFrame(HitTestKeyFrame(position));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_draggingKeyFrame is null)
        {
            SetHoveredKeyFrame(null);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;

        Point mousePosition = e.GetPosition(this);

        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            ZoomAtMouse(mousePosition.X, e.Delta);
            SetHoveredKeyFrame(HitTestKeyFrame(mousePosition));
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            double deltaX = e.Delta * HorizontalScrollSpeed;
            SetViewportLocation(new Point(ViewportLocation.X - deltaX, ViewportLocation.Y));
            SetHoveredKeyFrame(HitTestKeyFrame(mousePosition));
            return;
        }

        double deltaY = e.Delta * VerticalScrollSpeed;
        SetViewportLocation(new Point(ViewportLocation.X, ViewportLocation.Y - deltaY));
        SetHoveredKeyFrame(HitTestKeyFrame(mousePosition));
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var surface = (TimelineRenderSurface)d;
        surface._selectedKeyFrames.Clear();
        surface._hoveredKeyFrame = null;
        surface._draggingKeyFrame = null;
        surface.ReleaseMouseCapture();
        surface.InvalidateVisual();
    }

    private static void OnScrollableBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var surface = (TimelineRenderSurface)d;
        surface.CoerceViewportLocation();
        surface.InvalidateVisual();
    }

    private void ZoomAtMouse(double mouseX, int wheelDelta)
    {
        double oldZoom = Zoom;
        double zoomStep = Math.Max(0.0001, ZoomStep);
        double newZoom = oldZoom * (wheelDelta > 0 ? 1 + zoomStep : 1 / (1 + zoomStep));
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - oldZoom) < 0.001)
        {
            return;
        }

        double zoomRatio = newZoom / oldZoom;
        double newViewportX = (ViewportLocation.X + mouseX) * zoomRatio - mouseX;

        Zoom = newZoom;
        SetViewportLocation(new Point(newViewportX, ViewportLocation.Y));
    }

    private void SetViewportLocation(Point value)
    {
        ViewportLocation = new Point(Math.Max(MinViewportX, value.X), Math.Clamp(value.Y, 0, GetMaxViewportY()));
    }

    private void CoerceViewportLocation()
    {
        SetViewportLocation(ViewportLocation);
    }

    private double GetMaxViewportY()
    {
        return Math.Max(0, ContentHeight - ActualHeight);
    }

    private void ProcessDrag(MouseEventArgs e)
    {
        if (_draggingKeyFrame is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _draggingKeyFrame = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            return;
        }

        double pixelsPerTick = PixelPerTick * Zoom;
        if (pixelsPerTick <= 0)
        {
            return;
        }

        Point position = e.GetPosition(this);
        double deltaX = position.X - _dragStartMousePosition.X;
        int tickDelta = (int)Math.Round(deltaX / pixelsPerTick, MidpointRounding.AwayFromZero);
        int newTick = Math.Max(0, _dragStartTick + tickDelta);

        if (_draggingKeyFrame.Value.KeyFrame.Tick != newTick)
        {
            _draggingKeyFrame.Value.KeyFrame.Tick = newTick;
            InvalidateVisual();
        }
    }

    private void SetHoveredKeyFrame(TimelineKeyFrameRef? keyFrame)
    {
        if (AreSameKeyFrames(_hoveredKeyFrame, keyFrame))
        {
            return;
        }

        _hoveredKeyFrame = keyFrame;
        Cursor = keyFrame is null ? null : Cursors.SizeWE;
        InvalidateVisual();
    }

    private TimelineKeyFrameRef? HitTestKeyFrame(Point position)
    {
        if (Chart is not { } chart || LayoutProvider is not { } layoutProvider)
        {
            return null;
        }

        double pixelsPerTick = PixelPerTick * Zoom;
        double visibleTickStart = Math.Max(0, ViewportLocation.X / Math.Max(0.000001, pixelsPerTick));
        double visibleTickEnd = (ViewportLocation.X + ActualWidth) / Math.Max(0.000001, pixelsPerTick);

        TimelineKeyFrameRef? hit = null;
        foreach (var keyFrameRef in EnumerateVisibleKeyFrames(chart, layoutProvider, visibleTickStart, visibleTickEnd))
        {
            if (GetKeyFrameBounds(keyFrameRef, layoutProvider, ViewportLocation, pixelsPerTick).Contains(position))
            {
                hit = keyFrameRef;
            }
        }

        return hit;
    }

    private bool SelectKeyFrame(TimelineKeyFrameRef keyFrameRef, bool toggle)
    {
        if (toggle)
        {
            if (_selectedKeyFrames.Contains(keyFrameRef.KeyFrame))
            {
                _selectedKeyFrames.Remove(keyFrameRef.KeyFrame);
                return false;
            }

            _selectedKeyFrames.Add(keyFrameRef.KeyFrame);
            return true;
        }

        _selectedKeyFrames.Clear();
        _selectedKeyFrames.Add(keyFrameRef.KeyFrame);
        return true;
    }

    private void DrawLine(
        DrawingContext drawingContext,
        JudgementLine line,
        ITrackLayoutProvider layoutProvider,
        Point viewport,
        Rect viewportRect,
        double pixelsPerTick)
    {
        double x = line.StartTick * pixelsPerTick;
        double y = layoutProvider.GetLineYCoordinate(line);
        double width = line.DurationTicks * pixelsPerTick;
        var rect = new Rect(x - viewport.X, y - viewport.Y, Math.Max(0, width), layoutProvider.BaseTrackHeight);

        if (!rect.IntersectsWith(viewportRect))
        {
            return;
        }

        drawingContext.DrawRoundedRectangle(LineFill, LineStroke, rect, 2, 2);
    }

    private void DrawKeyFrames(
        DrawingContext drawingContext,
        JudgementLine line,
        ITrackLayoutProvider layoutProvider,
        Point viewport,
        Rect viewportRect,
        double visibleTickStart,
        double visibleTickEnd,
        double pixelsPerTick)
    {
        foreach (var keyFrameRef in EnumerateVisibleKeyFrames(line, layoutProvider, visibleTickStart, visibleTickEnd))
        {
            DrawKeyFrame(drawingContext, keyFrameRef, layoutProvider, viewport, viewportRect, pixelsPerTick);
        }
    }

    private void DrawKeyFrame(
        DrawingContext drawingContext,
        TimelineKeyFrameRef keyFrameRef,
        ITrackLayoutProvider layoutProvider,
        Point viewport,
        Rect viewportRect,
        double pixelsPerTick)
    {
        Rect bounds = GetKeyFrameBounds(keyFrameRef, layoutProvider, viewport, pixelsPerTick);
        if (!bounds.IntersectsWith(viewportRect))
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(bounds.X + HalfKeyFrameSize, bounds.Y), isFilled: true, isClosed: true);
            context.LineTo(new Point(bounds.X + KeyFrameSize, bounds.Y + HalfKeyFrameSize), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(bounds.X + HalfKeyFrameSize, bounds.Y + KeyFrameSize), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(bounds.X, bounds.Y + HalfKeyFrameSize), isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();

        Brush fill = KeyFrameFill;
        Pen stroke = KeyFrameStroke;

        if (_selectedKeyFrames.Contains(keyFrameRef.KeyFrame))
        {
            fill = KeyFrameSelectedFill;
            stroke = KeyFrameSelectedStroke;
        }

        if (AreSameKeyFrames(_hoveredKeyFrame, keyFrameRef))
        {
            fill = KeyFrameHoverFill;
            stroke = KeyFrameHoverStroke;
        }

        if (AreSameKeyFrames(_draggingKeyFrame, keyFrameRef))
        {
            fill = KeyFrameDraggingFill;
            stroke = KeyFrameDraggingStroke;
        }

        drawingContext.DrawGeometry(fill, stroke, geometry);
    }

    private Rect GetKeyFrameBounds(TimelineKeyFrameRef keyFrameRef, ITrackLayoutProvider layoutProvider, Point viewport, double pixelsPerTick)
    {
        double propertyY = layoutProvider.GetPropertyYCoordinate(keyFrameRef.Line, keyFrameRef.PropertyName);
        double x = keyFrameRef.KeyFrame.Tick * pixelsPerTick - HalfKeyFrameSize;
        double y = propertyY + layoutProvider.PropertyRowHeight / 2.0 - HalfKeyFrameSize;
        return new Rect(x - viewport.X, y - viewport.Y, KeyFrameSize, KeyFrameSize);
    }

    private IEnumerable<TimelineKeyFrameRef> EnumerateVisibleKeyFrames(
        Chart chart,
        ITrackLayoutProvider layoutProvider,
        double visibleTickStart,
        double visibleTickEnd)
    {
        foreach (var line in chart.JudgementLines)
        {
            if (line.StartTick > visibleTickEnd || line.StartTick + line.DurationTicks < visibleTickStart)
            {
                continue;
            }

            foreach (var keyFrame in EnumerateVisibleKeyFrames(line, layoutProvider, visibleTickStart, visibleTickEnd))
            {
                yield return keyFrame;
            }
        }
    }

    private IEnumerable<TimelineKeyFrameRef> EnumerateVisibleKeyFrames(
        JudgementLine line,
        ITrackLayoutProvider layoutProvider,
        double visibleTickStart,
        double visibleTickEnd)
    {
        foreach (string propertyName in TimelinePropertyNames)
        {
            double propertyY = layoutProvider.GetPropertyYCoordinate(line, propertyName);
            if (propertyY < 0)
            {
                continue;
            }

            foreach (var keyFrame in GetKeyFrames(line, propertyName))
            {
                if (keyFrame.Tick >= visibleTickStart && keyFrame.Tick <= visibleTickEnd)
                {
                    yield return new TimelineKeyFrameRef(line, propertyName, keyFrame);
                }
            }
        }
    }

    private static IEnumerable<IKeyFrame> GetKeyFrames(JudgementLine line, string propertyName)
    {
        return propertyName switch
        {
            "Position" => line.Properties.Position.KeyFrames,
            "Scale" => line.Properties.Scale.KeyFrames,
            "Rotation" => line.Properties.Rotation.KeyFrames,
            "Opacity" => line.Properties.Opacity.KeyFrames,
            "Speed" => line.Properties.Speed.KeyFrames,
            _ => []
        };
    }

    private static bool AreSameKeyFrames(TimelineKeyFrameRef? left, TimelineKeyFrameRef? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return ReferenceEquals(left.Value.KeyFrame, right.Value.KeyFrame);
    }

    private static bool AreSameKeyFrames(TimelineKeyFrameRef? left, TimelineKeyFrameRef right)
    {
        return left is not null && ReferenceEquals(left.Value.KeyFrame, right.KeyFrame);
    }

    private static readonly string[] TimelinePropertyNames =
    [
        "Position",
        "Scale",
        "Rotation",
        "Opacity",
        "Speed"
    ];

    private readonly record struct TimelineKeyFrameRef(JudgementLine Line, string PropertyName, IKeyFrame KeyFrame);
}
