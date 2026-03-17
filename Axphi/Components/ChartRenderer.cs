using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        

        // 🌟 1. 静态缓存音符图片 (路径里的 pack://application:,,,/ 是 WPF 的标准资源路径写法)
        // 请把 Resources/Notes/... 换成你实际的文件夹和文件名！
        private static readonly BitmapImage _imgTap = new BitmapImage(new Uri("pack://application:,,,/Resources/Notes/tap.png"));
        private static readonly BitmapImage _imgDrag = new BitmapImage(new Uri("pack://application:,,,/Resources/Notes/drag.png"));
        private static readonly BitmapImage _imgHoldHead = new BitmapImage(new Uri("pack://application:,,,/Resources/Notes/hold.png"));
        private static readonly BitmapImage _imgFlick = new BitmapImage(new Uri("pack://application:,,,/Resources/Notes/flick.png"));

        // 2. 声明 Hold 专属的三段画刷
        private static readonly ImageBrush _brushHoldTail;
        private static readonly ImageBrush _brushHoldBody;
        private static readonly ImageBrush _brushHoldHead;


        // 3. 用静态构造函数初始化切图
        static ChartRenderer()
        {
            var imgHold = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hold.png", UriKind.Absolute));

            // Viewbox 的四个参数分别是：X比例, Y比例, 宽比例, 高比例
            // 因为图片高150，三等分的话，每份高 50/150 = 1/3
            // 上：0 ~ 1/3 (尾巴)
            _brushHoldTail = new ImageBrush(imgHold) { Viewbox = new Rect(0, 0, 1, 1.0 / 3.0), ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
            // 中：1/3 ~ 2/3 (拉伸身体)
            _brushHoldBody = new ImageBrush(imgHold) { Viewbox = new Rect(0, 1.0 / 3.0, 1, 1.0 / 3.0), ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
            // 下：2/3 ~ 1 (头部)
            _brushHoldHead = new ImageBrush(imgHold) { Viewbox = new Rect(0, 2.0 / 3.0, 1, 1.0 / 3.0), ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };

            // 冻结画刷以获得极限渲染性能
            _brushHoldTail.Freeze();
            _brushHoldBody.Freeze();
            _brushHoldHead.Freeze();
        }
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
        // ================= 核心：数值积分求真实下落距离 =================
        private static double CalculateIntegralDistance(int startTick, int endTick, JudgementLine line, Chart chart)
        {
            if (startTick == endTick) return 0;

            int steps = 15; // 15 段微积分切片，足以在 60fps 下骗过人类的眼睛
            double totalDistance = 0;

            int tMin = Math.Min(startTick, endTick);
            int tMax = Math.Max(startTick, endTick);

            double stepTick = (double)(tMax - tMin) / steps;

            for (int i = 0; i < steps; i++)
            {
                // 1. 切割出极小的时间片段
                int t1 = (int)Math.Round(tMin + i * stepTick);
                int t2 = (int)Math.Round(tMin + (i + 1) * stepTick);

                double sec1 = TimeTickConverter.TickToTime(t1, chart.BpmKeyFrames, chart.InitialBpm);
                double sec2 = TimeTickConverter.TickToTime(t2, chart.BpmKeyFrames, chart.InitialBpm);

                // 2. 抓取这个时间片段【正中间】的那一瞬间的速度（直接白嫖你写好的神兽方法）
                int midTick = (t1 + t2) / 2;
                EasingUtils.CalculateObjectSingleTransform(
                    midTick,
                    chart.KeyFrameEasingDirection,
                    line.InitialSpeed,
                    line.SpeedKeyFrames,
                    Axphi.Utilities.MathUtils.Lerp,
                    out var midSpeed);

                // 3. 微小距离 = 瞬间速度 × 微小时间，然后累加！
                totalDistance += midSpeed * (sec2 - sec1);
            }

            return startTick <= endTick ? totalDistance : -totalDistance;
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
            drawingContext.PushOpacity(opacity / 100);

            double lineLength = renderInfo.CanvasHeight * 5.76;
            double thickness = renderInfo.CanvasHeight * 0.0075;
            
            drawingContext.DrawRectangle(_lineYellow, null, new Rect(-lineLength / 2, -thickness / 2, lineLength, thickness));
            drawingContext.Pop(); // note 不继承 line 的 opacity
            if (line.Notes is { } notes)
            {
                foreach (var note in notes)
                {

                    // 传递 currentTick
                    //RenderNote(drawingContext, renderInfo, chart, note, currentTick, line.InitialSpeed); // speed 默认: 1, 渲染器宽 16 , 高 9
                    // ✅ 新代码：直接把 line 传给它
                    RenderNote(drawingContext, renderInfo, chart, note, currentTick, line);
                }
            }

            
            drawingContext.Pop();
        }

        private static void RenderNote(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, Note note, int currentTick, JudgementLine line)
        {
            var ticksFromNow = note.HitTime - currentTick;

            // 绝对值 > 1000 Tick 就不渲染，优化性能
            if (Math.Abs(ticksFromNow) > 1000) return;

            // ================= 1. 提取基础时间和实时速度 (不管什么模式都得先拿出来) =================
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            double hitTimeSeconds = TimeTickConverter.TickToTime(note.HitTime, chart.BpmKeyFrames, chart.InitialBpm);

            // 提取当前瞬间的实时速度 (如果是 Realtime 模式才需要算)
            double currentRealtimeSpeed = line.InitialSpeed;
            if (line.SpeedMode == "Realtime" && !note.CustomSpeed.HasValue)
            {
                EasingUtils.CalculateObjectSingleTransform(
                    currentTick, chart.KeyFrameEasingDirection,
                    line.InitialSpeed, line.SpeedKeyFrames,
                    Axphi.Utilities.MathUtils.Lerp, out currentRealtimeSpeed);
            }

            // ================= 2. 算术题：音符的真实物理下落距离 =================
            double distance = 0;
            if (note.CustomSpeed.HasValue)
            {
                // 独立匀速
                distance = note.CustomSpeed.Value * (hitTimeSeconds - currentSeconds);
            }
            else if (line.SpeedMode == "Realtime")
            {
                // 实时模式：当前瞬间速度直接乘
                distance = currentRealtimeSpeed * (hitTimeSeconds - currentSeconds);
            }
            else
            {
                // 积分模式：微积分算真实距离
                distance = CalculateIntegralDistance(currentTick, note.HitTime, line, chart);
            }

            // 距离取反（因为在屏幕坐标系里，未来是从上方往 Y 的负方向掉落的）
            distance = -distance;
            var pixelDistance = renderInfo.ChartUnitToPixel(distance);

            // ================= 3. 计算音符本体的变换和透明度 =================
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
            new TranslateTransform(pixelOffset.X, pixelOffset.Y + pixelDistance), // 👈 这里用到了 pixelDistance
        }
            };

            var currentKind = KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, currentTick, note.InitialKind);

            // ================= 4. 推入画板变换 =================
            drawingContext.PushTransform(noteTransform);
            drawingContext.PushOpacity(opacity / 100);

            // ================= 5. 开始画图 =================
            // ================= 5. 开始画图 =================
            if (currentKind == NoteKind.Hold)
            {
                // 算术题：算出 Hold 的物理像素长度
                double endTimeSeconds = TimeTickConverter.TickToTime(note.HitTime + note.HoldDuration, chart.BpmKeyFrames, chart.InitialBpm);
                double holdDistance = 0;

                if (note.CustomSpeed.HasValue)
                {
                    holdDistance = note.CustomSpeed.Value * (endTimeSeconds - hitTimeSeconds);
                }
                else if (line.SpeedMode == "Realtime")
                {
                    // 实时模式：用刚刚拿到的 currentRealtimeSpeed 乘
                    holdDistance = currentRealtimeSpeed * (endTimeSeconds - hitTimeSeconds);
                }
                else
                {
                    // 积分模式
                    holdDistance = CalculateIntegralDistance(note.HitTime, note.HitTime + note.HoldDuration, line, chart);
                }

                // 🌟 【修复核心】：侦测真实物理距离是否为负数！(负数说明速度是倒退的)
                bool isFlipped = holdDistance < 0;

                // 转成像素长度并取绝对值，保证几何算出来始终是正数面积
                double holdPixelLength = renderInfo.ChartUnitToPixel(Math.Abs(holdDistance));

                // 算出各个方块的尺寸
                double notePixelWidth = renderInfo.ChartUnitToPixel(2);
                double partHeight = notePixelWidth * (50.0 / 989.0);

                if (holdPixelLength < partHeight) holdPixelLength = partHeight;

                double bodyHeight = holdPixelLength - partHeight;
                if (bodyHeight < 0) bodyHeight = 0;

                // 摆放积木并加 1 像素重叠防锯齿 (默认全部往 -Y 方向画)
                Rect headRect = new Rect(-notePixelWidth / 2, -partHeight / 2, notePixelWidth, partHeight);
                Rect tailRect = new Rect(-notePixelWidth / 2, -holdPixelLength - partHeight / 2, notePixelWidth, partHeight);

                double overlap = 1.0;
                Rect bodyRect = new Rect(
                    -notePixelWidth / 2,
                    -holdPixelLength + partHeight / 2 - overlap,
                    notePixelWidth,
                    bodyHeight + overlap * 2
                );

                // ================= 🌟 魔法施法时刻 =================
                // 如果速度是负的，加一个 Y 轴镜像翻转画笔！
                // 这样原本画在上方的尾巴，会被瞬间翻转到下方，乖乖拖在音符头的后面！
                if (isFlipped)
                {
                    drawingContext.PushTransform(new ScaleTransform(1, -1));
                }

                // 依次画出三段
                drawingContext.DrawRectangle(_brushHoldBody, null, bodyRect);
                drawingContext.DrawRectangle(_brushHoldTail, null, tailRect);
                drawingContext.DrawRectangle(_brushHoldHead, null, headRect);

                // 🌟 画完一定要把镜像魔法撤销，防止影响后面的绘制
                if (isFlipped)
                {
                    drawingContext.Pop();
                }
                // ===================================================
            }
            else
            {
                // 普通音符，自适应渲染
                ImageSource imgSrc = currentKind switch
                {
                    NoteKind.Tap => _imgTap,
                    NoteKind.Drag => _imgDrag,
                    NoteKind.Flick => _imgFlick,
                    _ => _imgTap,
                };

                var aspectRatio = imgSrc.Height / imgSrc.Width;
                var notePixelWidth = renderInfo.ChartUnitToPixel(2);
                var notePixelHeight = notePixelWidth * aspectRatio;

                drawingContext.DrawImage(imgSrc, new Rect(-notePixelWidth / 2, -notePixelHeight / 2, notePixelWidth, notePixelHeight));
            }

            drawingContext.Pop(); // opacity
            drawingContext.Pop(); // transform
        }
    }
}
