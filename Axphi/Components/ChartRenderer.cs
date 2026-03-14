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
        private static SolidColorBrush _lineYellow = new SolidColorBrush(Color.FromRgb(254, 255, 169)); // #feffa9
        private static SolidColorBrush _lineBlue = new SolidColorBrush(Color.FromRgb(162, 238, 255));   // #a2eeff
        private static SolidColorBrush _lineWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));  // #ffffff

        private static SolidColorBrush _progressWhite = new SolidColorBrush(Color.FromRgb(145, 145, 145));
        private static SolidColorBrush _progressHeadWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));

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

            //var time = Time;
            //var renderInfo = CalculateRenderInfo();
            // 1. 获取物理现实时间
            var realTime = Time;
            var renderInfo = CalculateRenderInfo();

            // 2. 将现实时间转换为底层逻辑运算用的 Tick (int)
            int currentTick = CalculateCurrentTick(realTime, chart);

            

            //if (time > chart.Duration)
            //{
            //    time = chart.Duration;
            //}

            //RenderProgress(drawingContext, renderInfo, time / chart.Duration);

            // 4. 进度条计算，注意要强制转换为 double，防止整数除法结果为 0
            double progress = chart.Duration == 0 ? 0 : (double)currentTick / chart.Duration;
            RenderProgress(drawingContext, renderInfo, progress);

            var transformToCenter = new TranslateTransform(renderInfo.CanvasWidth / 2, renderInfo.CanvasHeight / 2);
            drawingContext.PushTransform(transformToCenter);

            foreach (var judgementLine in judgementLines)
            {
                //RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, time);
                // 5. 往下传的是 currentTick (int)
                RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, currentTick);
            }

            drawingContext.Pop();
        }

        /// <summary>
        /// 将播放器的 TimeSpan 物理时间实时换算为 谱面系统的 Tick (128分音符数量)
        /// </summary>
        private static int CalculateCurrentTick(TimeSpan realTime, Chart chart)
        {
            // 积分计算
            double exactTick = TimeTickConverter.TimeToTick(realTime.TotalSeconds, chart.BpmKeyFrames, chart.InitialBpm); 
            double absoluteTick = exactTick + chart.Offset;

            // 保持全宇宙统一的四舍五入！
            return (int)Math.Round(absoluteTick, MidpointRounding.AwayFromZero);
        }

        private static void RenderProgress(DrawingContext drawingContext, RenderInfo renderInfo, double progress)
        {
            double height = renderInfo.CanvasHeight / 90;                   // 12 / 1080
            double headWidth = renderInfo.CanvasHeight / 360;               // 3 / 1080
            double progressPixel = renderInfo.CanvasWidth * progress;
            double progressPixelForHead = progressPixel * (1923.0 / 1920.0);
            
            drawingContext.DrawRectangle(_progressWhite, null, new Rect(0, 0, progressPixel, height));
            drawingContext.DrawRectangle(_progressHeadWhite, null, new Rect(progressPixelForHead - headWidth, 0, headWidth, height));
        }

        private static void RenderBpmLines(DrawingContext drawingContext, RenderInfo renderInfo)
        {

        }

        private static void RenderJudgementLine(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, JudgementLine line, int currentTick)
        {
            EasingUtils.CalculateObjectTransform(
                currentTick, chart.KeyFrameEasingDirection,
                line.AnimatableProperties,
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

            double lineLength = renderInfo.CanvasHeight * 5.76;
            double thickness = renderInfo.CanvasHeight * 0.0075;
            
            drawingContext.DrawRectangle(_lineYellow, null, new Rect(-renderInfo.CanvasWidth / 2, -thickness / 2, lineLength, thickness));

            if (line.Notes is { } notes)
            {
                foreach (var note in notes)
                {
                    //RenderNote(drawingContext, renderInfo, chart, note, time, line.Speed);
                    // 传递 currentTick
                    RenderNote(drawingContext, renderInfo, chart, note, currentTick, line.Speed);
                }
            }

            drawingContext.Pop();
            drawingContext.Pop();
        }

        private static void RenderNote(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, Note note, int currentTick, double speed)
        {
            //var timeFromNow = note.HitTime - currentTick;
            //var timeSecToNow = timeFromNow.TotalSeconds;

            // 现在的 HitTime 是 int, currentTick 也是 int
            var ticksFromNow = note.HitTime - currentTick;

            //if (Math.Abs(timeSecToNow) > 10)
            //{
            //    return;
            //}
            // 旧版是绝对值 > 10秒 就不渲染。10秒在120BPM下大约是 640个Tick。我们给个宽裕的 1000。
            if (Math.Abs(ticksFromNow) > 1000)
            {
                return;
            }

            var finalSpeed = note.CustomSpeed ?? speed;

            

            
            // ================= ✨ 核心修复 2：音符的真实物理下落距离 =================
            // 我们必须知道【现在】是第几秒，【音符该被打中】是第几秒
            // 两者的真实时间差，乘以固定速度，才是它在屏幕上绝对正确的距离！
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            double hitTimeSeconds = TimeTickConverter.TickToTime(note.HitTime, chart.BpmKeyFrames, chart.InitialBpm);

            double secondsFromNow = hitTimeSeconds - currentSeconds;

            // 4. 用秒数去乘以速度 (这样 Speed 就依然是 "单位/秒" 的含义了)
            var distance = -finalSpeed * secondsFromNow;

            
            var pixelDistance = renderInfo.ChartUnitToPixel(distance);

            EasingUtils.CalculateObjectTransform(
                currentTick, chart.KeyFrameEasingDirection,
                note.AnimatableProperties,
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
