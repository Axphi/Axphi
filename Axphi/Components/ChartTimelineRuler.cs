using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;
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
using Axphi.Data;

namespace Axphi.Components
{
    /// <summary>
    /// 按照步骤 1a 或 1b 操作，然后执行步骤 2 以在 XAML 文件中使用此自定义控件。
    ///
    /// 步骤 1a) 在当前项目中存在的 XAML 文件中使用该自定义控件。
    /// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
    /// 元素中:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Axphi.Components"
    ///
    ///
    /// 步骤 1b) 在其他项目中存在的 XAML 文件中使用该自定义控件。
    /// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
    /// 元素中:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Axphi.Components;assembly=Axphi.Components"
    ///
    /// 您还需要添加一个从 XAML 文件所在的项目到此项目的项目引用，
    /// 并重新生成以避免编译错误:
    ///
    ///     在解决方案资源管理器中右击目标项目，然后依次单击
    ///     “添加引用”->“项目”->[浏览查找并选择此项目]
    ///
    ///
    /// 步骤 2)
    /// 继续操作并在 XAML 文件中使用控件。
    ///
    ///     <MyNamespace:ChartTimlineRuler/>
    ///
    /// </summary>
    public class ChartTimelineRuler : Control
    {
        static ChartTimelineRuler()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChartTimelineRuler), new FrameworkPropertyMetadata(typeof(ChartTimelineRuler)));
        }

        private const double DefaultHeaderWidth = 103;
        private static readonly TimeSpan[] _majorTickIntervals =
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
        };

        private static readonly Brush _rulerBackground = CreateFrozenBrush(Color.FromRgb(30, 30, 30));
        private static readonly Brush _rulerTextBrush = CreateFrozenBrush(Color.FromRgb(224, 224, 224));
        private static readonly Pen _majorTickPen = CreateFrozenPen(Color.FromRgb(172, 214, 255), 1);
        private static readonly Pen _minorTickPen = CreateFrozenPen(Color.FromRgb(92, 92, 92), 1);
        private static readonly Pen _bottomBorderPen = CreateFrozenPen(Color.FromRgb(70, 70, 70), 1);

        public double RulerHeight => 24;


        public ChartTimeline Context
        {
            get { return (ChartTimeline)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        public bool AllowTapAdjustPlayTime
        {
            get { return (bool)GetValue(AllowTapAdjustPlayTimeProperty); }
            set { SetValue(AllowTapAdjustPlayTimeProperty, value); }
        }


        public static readonly DependencyProperty ContextProperty =
            DependencyProperty.Register(nameof(Context), typeof(ChartTimeline), typeof(ChartTimelineRuler), new PropertyMetadata(null, OnContextChanged));

        public static readonly DependencyProperty AllowTapAdjustPlayTimeProperty =
            DependencyProperty.Register(nameof(AllowTapAdjustPlayTime), typeof(bool), typeof(ChartTimelineRuler), new PropertyMetadata(false));

        private static void OnContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ChartTimelineRuler ruler)
            {
                return;
            }

            if (e.OldValue is ChartTimeline oldContext)
            {
                oldContext.ViewportChanged -= ContextViewportChanged;
            }

            if (e.NewValue is ChartTimeline newContext)
            {
                newContext.ViewportChanged += ContextViewportChanged;
            }

            ruler.InvalidateVisual();
        }

        private static void ContextViewportChanged(object? sender, EventArgs e) => throw new NotImplementedException();

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (AllowTapAdjustPlayTime &&
                Context is { } context)
            {
                context.PlayTime = context.GetTimeAtTimelineX(e.GetPosition(this).X);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (AllowTapAdjustPlayTime &&
                IsMouseCaptured &&
                Context is { } context)
            {
                context.PlayTime = context.GetTimeAtTimelineX(e.GetPosition(this).X);
                e.Handled = true;
                return;
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            base.OnMouseUp(e);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (RenderSize.Width <= 0 || RenderSize.Height <= 0)
            {
                return;
            }

            var rulerRect = new Rect(0, 0, ActualWidth, RulerHeight);
            drawingContext.DrawRectangle(_rulerBackground, null, rulerRect);
            drawingContext.DrawLine(_bottomBorderPen, rulerRect.BottomLeft, rulerRect.BottomRight);

            drawingContext.PushClip(new RectangleGeometry(rulerRect));
            DrawTicks(drawingContext, Context, rulerRect);
            drawingContext.Pop();
        }

        private void DrawTicks(DrawingContext drawingContext, ChartTimeline context, Rect rulerRect)
        {
            if (context is null)
            {
                return;
            }

            var majorInterval = SelectMajorTickInterval(context);
            var minorInterval = SelectMinorTickInterval(context, majorInterval);

            DrawTickSeries(drawingContext, context, rulerRect, minorInterval, _minorTickPen, 6, drawLabels: false);
            DrawTickSeries(drawingContext, context, rulerRect, majorInterval, _majorTickPen, 12, drawLabels: true);
        }

        private void DrawTickSeries(DrawingContext drawingContext, ChartTimeline context, Rect rulerRect, TimeSpan interval, Pen pen, double tickHeight, bool drawLabels)
        {
            if (interval <= TimeSpan.Zero ||
                context is null)
            {
                return;
            }

            var startTicks = context.Time.Ticks;
            var endTicks = context.GetTimeAtTimelineX(rulerRect.Width).Ticks;
            var intervalTicks = interval.Ticks;
            var tick = FloorToInterval(startTicks, intervalTicks);

            while (tick <= endTicks + intervalTicks)
            {
                var tickTime = TimeSpan.FromTicks(tick);
                var x = rulerRect.X + context.GetTimelineX(tickTime);
                if (x >= rulerRect.X - 1 && x <= rulerRect.Right + 1)
                {
                    var tickStart = new Point(x, rulerRect.Bottom - tickHeight);
                    var tickEnd = new Point(x, rulerRect.Bottom);
                    drawingContext.DrawLine(pen, tickStart, tickEnd);

                    if (drawLabels)
                    {
                        DrawTickLabel(drawingContext, tickTime, x, rulerRect, interval);
                    }
                }

                tick += intervalTicks;
            }
        }

        private void DrawTickLabel(DrawingContext drawingContext, TimeSpan tickTime, double x, Rect rulerRect, TimeSpan interval)
        {
            var label = FormatTime(tickTime, interval);
            var formattedText = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                _rulerTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var textPosition = new Point(x + 3, rulerRect.Y + 2);
            drawingContext.DrawText(formattedText, textPosition);
        }

        private TimeSpan SelectMajorTickInterval(ChartTimeline context)
        {
            foreach (var interval in _majorTickIntervals)
            {
                if (interval.TotalSeconds * context.EffectiveLengthPerSecond >= 72)
                {
                    return interval;
                }
            }

            return _majorTickIntervals[^1];
        }

        private TimeSpan SelectMinorTickInterval(ChartTimeline context, TimeSpan majorInterval)
        {
            var majorSeconds = majorInterval.TotalSeconds;
            foreach (var divisor in new[] { 10, 5, 4, 2 })
            {
                var minorSeconds = majorSeconds / divisor;
                if (minorSeconds * context.EffectiveLengthPerSecond >= 14)
                {
                    return TimeSpan.FromSeconds(minorSeconds);
                }
            }

            return majorInterval;
        }

        private static long FloorToInterval(long value, long interval)
        {
            var result = value / interval;
            if (value < 0 && value % interval != 0)
            {
                result -= 1;
            }

            return result * interval;
        }

        private static string FormatTime(TimeSpan time, TimeSpan interval)
        {
            var sign = time < TimeSpan.Zero ? "-" : string.Empty;
            var absoluteTime = time.Duration();

            if (interval < TimeSpan.FromSeconds(1))
            {
                return $"{sign}{absoluteTime:mm\\:ss\\.fff}";
            }

            if (absoluteTime >= TimeSpan.FromMinutes(1))
            {
                return $"{sign}{absoluteTime:mm\\:ss}";
            }

            return $"{sign}{absoluteTime.TotalSeconds:0}";
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen CreateFrozenPen(Color color, double thickness)
        {
            var pen = new Pen(CreateFrozenBrush(color), thickness);
            pen.Freeze();
            return pen;
        }
    }
}