using Axphi.Data;
using Axphi.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Axphi.Components;

public sealed class TrackHeader : FrameworkElement
{
    private static readonly Brush HeaderBackground = new SolidColorBrush(Color.FromRgb(74, 74, 74));
    private static readonly Brush HeaderHoverBackground = new SolidColorBrush(Color.FromRgb(86, 86, 86));
    private static readonly Brush PropertyBackground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(254, 255, 169));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238));
    private static readonly Brush SecondaryTextBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209));
    private static readonly Pen SeparatorPen = new Pen(new SolidColorBrush(Color.FromRgb(38, 38, 38)), 1.0);
    private static readonly Pen PropertyButtonPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 160, 160)), 1.2);
    private static readonly Brush HitTestFill = Brushes.Transparent;

    private JudgementLine? _hoveredLine;

    static TrackHeader()
    {
        HeaderBackground.Freeze();
        HeaderHoverBackground.Freeze();
        PropertyBackground.Freeze();
        AccentBrush.Freeze();
        TextBrush.Freeze();
        SecondaryTextBrush.Freeze();
        SeparatorPen.Freeze();
        PropertyButtonPen.Freeze();
    }

    public TrackHeader()
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
            typeof(TrackHeader),
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
            typeof(TrackHeader),
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
            typeof(TrackHeader),
            new FrameworkPropertyMetadata(
                default(Point),
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double VerticalScrollSpeed
    {
        get => (double)GetValue(VerticalScrollSpeedProperty);
        set => SetValue(VerticalScrollSpeedProperty, value);
    }

    public static readonly DependencyProperty VerticalScrollSpeedProperty =
        DependencyProperty.Register(nameof(VerticalScrollSpeed), typeof(double), typeof(TrackHeader), new PropertyMetadata(0.1));

    public double ContentHeight
    {
        get => (double)GetValue(ContentHeightProperty);
        set => SetValue(ContentHeightProperty, value);
    }

    public static readonly DependencyProperty ContentHeightProperty =
        DependencyProperty.Register(
            nameof(ContentHeight),
            typeof(double),
            typeof(TrackHeader),
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
            typeof(TrackHeader),
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

        var viewportRect = new Rect(0, 0, ActualWidth, ActualHeight);
        double viewportY = ViewportLocation.Y;
        double visibleYStart = viewportY;
        double visibleYEnd = viewportY + ActualHeight;

        for (int i = 0; i < chart.JudgementLines.Count; i++)
        {
            var line = chart.JudgementLines[i];
            double lineY = layoutProvider.GetLineYCoordinate(line);
            var activeProperties = layoutProvider.GetActiveProperties(line);
            double lineHeight = layoutProvider.BaseTrackHeight + activeProperties.Count * layoutProvider.PropertyRowHeight;

            if (lineY > visibleYEnd || lineY + lineHeight < visibleYStart)
            {
                continue;
            }

            DrawLine(drawingContext, line, i, activeProperties, layoutProvider, viewportY, viewportRect);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;

        double deltaY = e.Delta * VerticalScrollSpeed;
        SetViewportLocation(new Point(0, ViewportLocation.Y - deltaY));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        if (LayoutProvider is not { } layoutProvider)
        {
            return;
        }

        var hit = HitTestLineHeader(e.GetPosition(this), layoutProvider);
        if (hit is null)
        {
            return;
        }

        layoutProvider.ToggleLineExpanded(hit);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var hit = LayoutProvider is { } layoutProvider
            ? HitTestLineHeader(e.GetPosition(this), layoutProvider)
            : null;

        if (ReferenceEquals(_hoveredLine, hit))
        {
            return;
        }

        _hoveredLine = hit;
        Cursor = hit is null ? null : Cursors.Hand;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredLine = null;
        Cursor = null;
        InvalidateVisual();
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var header = (TrackHeader)d;
        header._hoveredLine = null;
        header.InvalidateVisual();
    }

    private static void OnScrollableBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var header = (TrackHeader)d;
        header.CoerceViewportLocation();
        header.InvalidateVisual();
    }

    private void SetViewportLocation(Point value)
    {
        ViewportLocation = new Point(0, Math.Clamp(value.Y, 0, GetMaxViewportY()));
    }

    private void CoerceViewportLocation()
    {
        SetViewportLocation(ViewportLocation);
    }

    private double GetMaxViewportY()
    {
        return Math.Max(0, ContentHeight - ActualHeight);
    }

    private JudgementLine? HitTestLineHeader(Point position, ITrackLayoutProvider layoutProvider)
    {
        if (Chart is null)
        {
            return null;
        }

        foreach (var line in Chart.JudgementLines)
        {
            double y = layoutProvider.GetLineYCoordinate(line) - ViewportLocation.Y;
            var rect = new Rect(0, y, ActualWidth, layoutProvider.BaseTrackHeight);
            if (rect.Contains(position))
            {
                return line;
            }
        }

        return null;
    }

    private void DrawLine(
        DrawingContext drawingContext,
        JudgementLine line,
        int index,
        IReadOnlyList<string> activeProperties,
        ITrackLayoutProvider layoutProvider,
        double viewportY,
        Rect viewportRect)
    {
        double y = layoutProvider.GetLineYCoordinate(line) - viewportY;
        double lineHeight = layoutProvider.BaseTrackHeight + activeProperties.Count * layoutProvider.PropertyRowHeight;
        var bounds = new Rect(0, y, ActualWidth, lineHeight);

        if (!bounds.IntersectsWith(viewportRect))
        {
            return;
        }

        var headerRect = new Rect(0, y, ActualWidth, layoutProvider.BaseTrackHeight);
        drawingContext.DrawRectangle(ReferenceEquals(line, _hoveredLine) ? HeaderHoverBackground : HeaderBackground, null, headerRect);
        drawingContext.DrawRectangle(AccentBrush, null, new Rect(0, y, 5, layoutProvider.BaseTrackHeight));
        drawingContext.DrawLine(SeparatorPen, new Point(0, y + layoutProvider.BaseTrackHeight), new Point(ActualWidth, y + layoutProvider.BaseTrackHeight));

        DrawArrow(drawingContext, new Rect(8, y + 5, 10, 10), activeProperties.Count > 0);
        DrawText(drawingContext, GetDisplayName(line, index), new Point(28, y + 2), TextBrush, 12);

        double propertyY = y + layoutProvider.BaseTrackHeight;
        foreach (string propertyName in activeProperties.ToArray())
        {
            DrawPropertyRow(drawingContext, propertyName, propertyY, layoutProvider.PropertyRowHeight);
            propertyY += layoutProvider.PropertyRowHeight;
        }
    }

    private void DrawPropertyRow(DrawingContext drawingContext, string propertyName, double y, double propertyRowHeight)
    {
        var rect = new Rect(0, y, ActualWidth, propertyRowHeight);
        drawingContext.DrawRectangle(PropertyBackground, null, rect);
        drawingContext.DrawLine(SeparatorPen, new Point(0, y + propertyRowHeight), new Point(ActualWidth, y + propertyRowHeight));

        DrawDiamond(drawingContext, new Rect(29, y + 4, 10, 10));
        DrawText(drawingContext, propertyName, new Point(48, y + 1), SecondaryTextBrush, 11);
    }

    private static string GetDisplayName(JudgementLine line, int index)
    {
        return string.IsNullOrWhiteSpace(line.Name) ? $"Line {index}" : line.Name;
    }

    private static void DrawArrow(DrawingContext drawingContext, Rect bounds, bool isExpanded)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (isExpanded)
            {
                context.BeginFigure(new Point(bounds.X, bounds.Y + 2), isFilled: true, isClosed: true);
                context.LineTo(new Point(bounds.X + bounds.Width, bounds.Y + 2), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height - 2), isStroked: true, isSmoothJoin: false);
            }
            else
            {
                context.BeginFigure(new Point(bounds.X + 2, bounds.Y), isFilled: true, isClosed: true);
                context.LineTo(new Point(bounds.X + bounds.Width - 2, bounds.Y + bounds.Height / 2), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.X + 2, bounds.Y + bounds.Height), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(SecondaryTextBrush, null, geometry);
    }

    private static void DrawDiamond(DrawingContext drawingContext, Rect bounds)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(bounds.X + bounds.Width / 2, bounds.Y), isFilled: true, isClosed: true);
            context.LineTo(new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height / 2), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(bounds.X, bounds.Y + bounds.Height / 2), isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, PropertyButtonPen, geometry);
    }

    private void DrawText(DrawingContext drawingContext, string text, Point origin, Brush brush, double fontSize)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(0, ActualWidth - origin.X - 4),
            Trimming = TextTrimming.CharacterEllipsis
        };

        drawingContext.DrawText(formattedText, origin);
    }
}
