using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;

namespace Axphi.Components
{
    public class ChartRenderer : FrameworkElement
    {
        private static SolidColorBrush _lineYellow = new SolidColorBrush(Color.FromRgb(254, 255, 169));
        private static SolidColorBrush _lineBlue = new SolidColorBrush(Color.FromRgb(162, 238, 255));
        private static SolidColorBrush _lineWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        private static SolidColorBrush _progressWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        private static SolidColorBrush _noteFlick = new SolidColorBrush(Color.FromRgb(255, 95, 95));
        private static SolidColorBrush _noteTap = new SolidColorBrush(Color.FromRgb(82, 133, 243));
        private static SolidColorBrush _noteDrag = new SolidColorBrush(Color.FromRgb(255, 222, 145));
        private static SolidColorBrush _noteHold = new SolidColorBrush(Color.FromRgb(81, 180, 255));


        public TimeSpan Time
        {
            get { return (TimeSpan)GetValue(TimeProperty); }
            set { SetValue(TimeProperty, value); }
        }

        public Chart Chart
        {
            get { return (Chart)GetValue(ChartProperty); }
            set { SetValue(ChartProperty, value); }
        }

        public bool ShowBpmLines
        {
            get { return (bool)GetValue(ShowBpmLinesProperty); }
            set { SetValue(ShowBpmLinesProperty, value); }
        }

        public int BpmLinesDivisor
        {
            get { return (int)GetValue(BpmLinesDivisorProperty); }
            set { SetValue(BpmLinesDivisorProperty, value); }
        }

        public static readonly DependencyProperty BpmLinesDivisorProperty =
            DependencyProperty.Register("BpmLinesDivisor", typeof(int), typeof(ChartRenderer), new PropertyMetadata(1));

        public static readonly DependencyProperty ShowBpmLinesProperty =
            DependencyProperty.Register("ShowBpmLines", typeof(bool), typeof(ChartRenderer), new PropertyMetadata(false));





        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register("Time", typeof(TimeSpan), typeof(ChartRenderer),
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ChartProperty =
            DependencyProperty.Register("Chart", typeof(Chart), typeof(ChartRenderer),
                new FrameworkPropertyMetadata(default(Chart), FrameworkPropertyMetadataOptions.AffectsRender));

        private record struct RenderInfo(
            double CanvasWidth, double CanvasHeight,
            double ClientWidth, double ClientHeight,
            double ChartUnitToPixelFactor)
        {
            public double ChartUnitToPixel(double chartSize)
            {
                return chartSize * ChartUnitToPixelFactor;
            }
        }

        /// <summary>
        /// 宽高比例 16:9, 谱面标准大小 16, 9
        /// </summary>
        /// <returns></returns>
        private RenderInfo CalculateRenderInfo()
        {
            var canvasWidth = RenderSize.Width;
            var canvasHeight = RenderSize.Height;
            var clientWidth = canvasWidth;
            var clientHeight = clientWidth / 16 * 9;
            if (clientHeight > canvasHeight)
            {
                clientHeight = canvasHeight;
                clientWidth = clientHeight / 9 * 16;
            }

            var chartUnitToPixelFactor = clientWidth / 16;

            return new RenderInfo(canvasWidth, canvasHeight, clientWidth, clientHeight, chartUnitToPixelFactor);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (Chart is not { } chart ||
                chart.JudgementLines is not { } judgementLines)
            {
                return;
            }

            var time = Time;
            var renderInfo = CalculateRenderInfo();

            if (time > chart.Duration)
            {
                time = chart.Duration;
            }

            RenderProgress(drawingContext, renderInfo, time / chart.Duration);

            var transformToCenter = new TranslateTransform(renderInfo.CanvasWidth / 2, renderInfo.CanvasHeight / 2);
            drawingContext.PushTransform(transformToCenter);

            foreach (var judgementLine in judgementLines)
            {
                RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, time);
            }

            drawingContext.Pop();
        }

        private static void RenderProgress(DrawingContext drawingContext, RenderInfo renderInfo, double progress)
        {
            drawingContext.DrawRectangle(_progressWhite, null, new Rect(0, renderInfo.CanvasHeight - 4, renderInfo.CanvasWidth * progress, 4));
        }

        private static void RenderBpmLines(DrawingContext drawingContext, RenderInfo renderInfo)
        {

        }

        private static void RenderJudgementLine(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, JudgementLine line, TimeSpan time)
        {
            EasingUtils.CalculateObjectTransform(
                time, chart.KeyFrameEasingDirection,
                line,
                out var offset, out var scale, out var rotationAngle, out var opacity);

            var pixelOffset = new Vector(
                renderInfo.ChartUnitToPixel(offset.X),
                renderInfo.ChartUnitToPixel(offset.Y));

            var transform = new TransformGroup()
            {
                Children =
                {
                    new ScaleTransform(scale.X, scale.Y),
                    new RotateTransform(-rotationAngle),
                    new TranslateTransform(pixelOffset.X, -pixelOffset.Y),
                }
            };

            drawingContext.PushTransform(transform);
            drawingContext.PushOpacity(opacity);

            drawingContext.DrawRectangle(_lineYellow, null, new Rect(-renderInfo.CanvasWidth / 2, -2, renderInfo.CanvasWidth, 4));

            if (line.Notes is { } notes)
            {
                foreach (var note in notes)
                {
                    RenderNote(drawingContext, renderInfo, chart, note, time, line.Speed);
                }
            }

            drawingContext.Pop();
            drawingContext.Pop();
        }

        private static void RenderNote(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, Note note, TimeSpan time, double speed)
        {
            var timeFromNow = note.HitTime - time;
            var timeSecToNow = timeFromNow.TotalSeconds;
            if (Math.Abs(timeSecToNow) > 10)
            {
                return;
            }

            var finalSpeed = note.CustomSpeed ?? speed;

            var distance = -finalSpeed * timeSecToNow;
            var pixelDistance = renderInfo.ChartUnitToPixel(distance);

            EasingUtils.CalculateObjectTransform(
                time, chart.KeyFrameEasingDirection,
                note,
                out var offset, out var scale, out var rotationAngle, out var opacity);

            var pixelOffset = new Vector(
                renderInfo.ChartUnitToPixel(offset.X),
                renderInfo.ChartUnitToPixel(offset.Y));

            var noteTransform = new TransformGroup()
            {
                Children =
                {
                    new ScaleTransform(scale.X, scale.Y),
                    new RotateTransform(rotationAngle),
                    new TranslateTransform(pixelOffset.X, pixelOffset.Y + pixelDistance),
                }
            };

            var fill = note.Kind switch
            {
                NoteKind.Tap => _noteTap,
                NoteKind.Drag => _noteDrag,
                NoteKind.Hold => _noteHold,
                NoteKind.Flick => _noteFlick,
                _ => Brushes.Purple,
            };

            var notePixelWidth = renderInfo.ChartUnitToPixel(2);
            var notePixelHeight = renderInfo.ChartUnitToPixel(0.2);

            drawingContext.PushTransform(noteTransform);
            drawingContext.PushOpacity(opacity);
            drawingContext.DrawRectangle(fill, null, new Rect(-notePixelWidth / 2, -notePixelHeight / 2, notePixelWidth, notePixelHeight));
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }
}
