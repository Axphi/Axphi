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
        private static readonly BitmapImage _imgTap = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/tap.png"));
        private static readonly BitmapImage _imgDrag = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/drag.png"));
        private static readonly BitmapImage _imgHoldHead = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hold.png"));
        private static readonly BitmapImage _imgFlick = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/flick.png"));


        // 在其他静态画刷声明的地方（例如 _brushHoldHead 下方）添加：
        private static readonly ImageSource[] _hitFxFrames = new ImageSource[30];
        // ================= 新增：Perfect 击打特效专属颜色 =================
        // 贴图颜色：rgba(255,236,160,0.8823529)，Alpha 0.88 * 255 ≈ 225
        private static readonly SolidColorBrush _perfectFxBrush = new SolidColorBrush(Color.FromArgb(225, 255, 236, 160));
        // =================================================================

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


            _perfectFxBrush.Freeze();

            // ================= 新增：初始化并切割击打特效 =================
            try
            {
                var hitFxSheet = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hit_fx.png", UriKind.Absolute));

                // 6行5列，共30帧，单帧尺寸 256x256
                for (int i = 0; i < 30; i++)
                {
                    int row = i / 5;
                    int col = i % 5;
                    var rect = new Int32Rect(col * 256, row * 256, 256, 256);
                    var cropped = new CroppedBitmap(hitFxSheet, rect);

                    // 冻结画刷以获得极限渲染性能
                    cropped.Freeze();
                    _hitFxFrames[i] = cropped;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("图片没找到: " + ex.Message);
            }
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
                // ================= 🌟 核心拦截：生死判定！ (性能起飞点) =================
                // 如果当前时间 < 图层的出生时间，或者 > 图层的死亡时间
                if (currentTick < judgementLine.StartTick || currentTick > (judgementLine.StartTick + judgementLine.DurationTicks))
                {
                    // 不在寿命范围内，直接跳过！
                    // 这根线，连同它肚子里的所有音符，不仅不画，连算都不用算了！
                    continue;
                }
                // =====================================================================



                //RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, time);
                // 5. 往下传的是 currentTick (int)
                RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, currentTick);
            }


            // ================= 新增：2. 最后在最上层画独立的击打特效 =================
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            RenderHitEffects(drawingContext, renderInfo, chart, currentTick, currentSeconds);
            // =====================================================================


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

            int steps = 150; // 150 段微积分切片，足以在 60fps 下骗过人类的眼睛, 你也可以根据性能需求调整这个数字，切得越细越准但越吃 CPU, 反之亦然, 150 是个比较稳妥的默认值, 既能保证大部分情况下的视觉效果，又不会对性能造成过大压力,  你可以在测试时尝试不同的切片数量，找到适合你项目的最佳平衡点, 甚至可以根据当前的帧率动态调整切片数量（帧率高时增加切片，帧率低时减少切片），以实现更智能的性能优化。
            // 注意：切片数量必须足够大，才能在速度变化剧烈的情况下保持视觉平滑，否则可能会出现明显的阶梯状跳跃感，尤其是在有急速加减速的段落。
            // 150 的经验值是基于一般的谱面设计和常见的速度变化模式得出的，通常能够在大多数情况下提供足够的平滑度，但如果你的谱面中存在极端的速度变化（例如突然的速度翻转或者非常短暂的加速/减速），你可能需要增加切片数量来确保视觉效果的平滑。
            // 最终，选择切片数量时需要权衡视觉效果和性能开销，建议在开发过程中进行测试和调整，以找到最适合你项目需求的切片数量。
            // 另外，如果你发现即使增加切片数量后仍然存在跳跃感，可能需要检查你的速度曲线是否存在极端的变化点，或者考虑在这些特殊情况下使用更高级的插值方法来进一步平滑速度变化。
            // 总之，150 是一个不错的起点，但根据你的具体情况进行调整和测试是非常重要的，以确保在保持良好视觉效果的同时不会对性能造成过大影响。
            // 切片数量过少的情况示例：如果你只有 10 个切片，那么每个切片覆盖的时间范围就会非常大，可能会跨越一个明显的速度变化点（例如从 1x 突然加速到 3x），在这种情况下，你计算出来的平均速度可能是 2x，但实际上在前半段时间里物体是以 1x 的速度移动的，在后半段时间里则是以 3x 的速度移动的，这样就会导致物体在视觉上出现明显的跳跃感，因为它没有正确地反映出速度变化的细节。
            // 切片数量过多的情况示例：如果你有 1000 个切片，那么每个切片覆盖的时间范围就会非常小，这样你就能够非常精确地捕捉到速度变化的细节，物体的移动会非常平滑，几乎没有任何跳跃感，但同时这也会增加计算量，可能会对性能造成较大的影响，尤其是在复杂的场景或者低性能设备上。
            // 因此，选择切片数量时需要根据你的具体需求进行权衡，既要保证足够的视觉平滑度，又要避免过度的性能开销。150 个切片通常是一个不错的平衡点，但你可以根据实际情况进行调整和测试，以找到最适合你项目的切片数量。
            // 总之，切片数量的选择对于实现平滑的视觉效果和保持良好的性能之间的平衡至关重要，建议在开发过程中进行测试和调整，以找到最适合你项目需求的切片数量。
            // 另外，如果你发现即使增加切片数量后仍然存在跳跃感，可能需要检查你的速度曲线是否存在极端的变化点，或者考虑在这些特殊情况下使用更高级的插值方法来进一步平滑速度变化。

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

            // ================= 1. 提前提取音符类型 =================
            var currentKind = KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, currentTick, note.InitialKind);

            // ================= 2. 视觉消失引擎 =================
            // Tap, Drag, Flick：只要越过判定线，直接消失！
            if (currentKind != NoteKind.Hold && currentTick >= note.HitTime) return;

            // Hold：只有当整条尾巴都彻底越过判定线时，才完全消失！
            if (currentKind == NoteKind.Hold && currentTick >= note.HitTime + note.HoldDuration) return;

            // ================= 3. 计算物理下落距离 (保持真实下落，绝不定住！) =================
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            double hitTimeSeconds = TimeTickConverter.TickToTime(note.HitTime, chart.BpmKeyFrames, chart.InitialBpm);

            double currentRealtimeSpeed = line.InitialSpeed;
            if (line.SpeedMode == "Realtime" && !note.CustomSpeed.HasValue)
            {
                EasingUtils.CalculateObjectSingleTransform(
                    currentTick, chart.KeyFrameEasingDirection,
                    line.InitialSpeed, line.SpeedKeyFrames,
                    Axphi.Utilities.MathUtils.Lerp, out currentRealtimeSpeed);
            }

            double distance = 0;
            if (note.CustomSpeed.HasValue) distance = note.CustomSpeed.Value * (hitTimeSeconds - currentSeconds);
            else if (line.SpeedMode == "Realtime") distance = currentRealtimeSpeed * (hitTimeSeconds - currentSeconds);
            else distance = CalculateIntegralDistance(currentTick, note.HitTime, line, chart);

            // 距离取反（屏幕上方是负方向）
            distance = -distance;
            var pixelDistance = renderInfo.ChartUnitToPixel(distance);

            // 计算音符本体的变换和透明度
            EasingUtils.CalculateObjectTransform(
                currentTick, chart.KeyFrameEasingDirection,
                note.AnimatableProperties,
                out var offset, out var scale, out var rotationAngle, out var opacity);

            var pixelOffset = new Vector(renderInfo.ChartUnitToPixel(offset.X), renderInfo.ChartUnitToPixel(offset.Y));

            var noteTransform = new TransformGroup()
            {
                Children = {
            new ScaleTransform(scale.X, scale.Y),
            new RotateTransform(rotationAngle),
            new TranslateTransform(pixelOffset.X, pixelOffset.Y + pixelDistance),
        }
            };

            // ================= 🌟 魔法点 1：施加隐形裁切蒙版 (Clip) 🌟 =================
            bool isHold = currentKind == NoteKind.Hold;
            bool isHoldPassed = isHold && currentTick >= note.HitTime;

            if (isHold)
            {
                // 我们在判定线（Y=0）的上方画一张无限大的隐形纸！
                // Rect 的参数：X, Y, Width, Height。
                // 从 Y = -100000 开始，高度 100000，正好切在 Y=0（判定线中轴线）的位置。
                // 只有在这个矩形范围内的画面才会被显示，越过判定线（Y>0）的部分直接被隐形！
                var clipGeometry = new RectangleGeometry(new Rect(-100000, -100000, 200000, 100000));
                drawingContext.PushClip(clipGeometry);
            }
            // =========================================================================

            drawingContext.PushTransform(noteTransform);
            drawingContext.PushOpacity(opacity / 100);

            // ================= 5. 开始画图 =================
            if (currentKind == NoteKind.Hold)
            {
                // 🌟 魔法点 2：恢复它真实的物理全长，不要去缩短它！
                // 因为有了蒙版，它无论多长，只要越过线就会被切掉。
                double endTimeSeconds = TimeTickConverter.TickToTime(note.HitTime + note.HoldDuration, chart.BpmKeyFrames, chart.InitialBpm);
                double holdDistance = 0;

                if (note.CustomSpeed.HasValue) holdDistance = note.CustomSpeed.Value * (endTimeSeconds - hitTimeSeconds);
                else if (line.SpeedMode == "Realtime") holdDistance = currentRealtimeSpeed * (endTimeSeconds - hitTimeSeconds);
                else holdDistance = CalculateIntegralDistance(note.HitTime, note.HitTime + note.HoldDuration, line, chart);

                bool isFlipped = holdDistance < 0;
                double holdPixelLength = renderInfo.ChartUnitToPixel(Math.Abs(holdDistance));

                double notePixelWidth = renderInfo.ChartUnitToPixel(1.95);
                double partHeight = notePixelWidth * (50.0 / 989.0);
                if (holdPixelLength < partHeight) holdPixelLength = partHeight;

                double bodyHeight = holdPixelLength - partHeight;
                if (bodyHeight < 0) bodyHeight = 0;

                Rect headRect = new Rect(-notePixelWidth / 2, -partHeight / 2, notePixelWidth, partHeight);
                Rect tailRect = new Rect(-notePixelWidth / 2, -holdPixelLength - partHeight / 2, notePixelWidth, partHeight);

                double overlap = 1.0;
                Rect bodyRect = new Rect(-notePixelWidth / 2, -holdPixelLength + partHeight / 2 - overlap, notePixelWidth, bodyHeight + overlap * 2);

                if (isFlipped) drawingContext.PushTransform(new ScaleTransform(1, -1));

                // 身体和尾巴照常画，越线的部位交给外层的蒙版去切
                drawingContext.DrawRectangle(_brushHoldBody, null, bodyRect);
                drawingContext.DrawRectangle(_brushHoldTail, null, tailRect);

                // 🌟 魔法点 3：如果越线了，"头"直接完全消失，不画了！
                if (!isHoldPassed)
                {
                    drawingContext.DrawRectangle(_brushHoldHead, null, headRect);
                }

                if (isFlipped) drawingContext.Pop();
            }
            else
            {
                ImageSource imgSrc = currentKind switch
                {
                    NoteKind.Tap => _imgTap,
                    NoteKind.Drag => _imgDrag,
                    NoteKind.Flick => _imgFlick,
                    _ => _imgTap,
                };

                var aspectRatio = imgSrc.Height / imgSrc.Width;
                var notePixelWidth = renderInfo.ChartUnitToPixel(1.95);
                var notePixelHeight = notePixelWidth * aspectRatio;

                drawingContext.DrawImage(imgSrc, new Rect(-notePixelWidth / 2, -notePixelHeight / 2, notePixelWidth, notePixelHeight));
            }



            drawingContext.Pop(); // opacity
            drawingContext.Pop(); // transform

            // ================= 撤销蒙版 =================
            if (isHold)
            {
                // 有借有还，如果是 Hold，必须把刚才 Push 进去的 Clip 给 Pop 出来
                drawingContext.Pop();
            }
        }

        private static void RenderHitEffects(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, int currentTick, double currentSeconds)
        {
            if (chart.JudgementLines == null) return;

            double noteScaleRatio = renderInfo.ClientWidth / 8080.0;
            bool enableParticleRotation = false; // 粒子自转开关

            foreach (var line in chart.JudgementLines)
            {
                if (line.Notes == null) continue;

                foreach (var note in line.Notes)
                {
                    // 提取音符类型和基础时间
                    var currentKind = KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, note.HitTime, note.InitialKind);
                    double hitTimeSeconds = TimeTickConverter.TickToTime(note.HitTime, chart.BpmKeyFrames, chart.InitialBpm);
                    double fxDurationSeconds = 0.5; // 特效持续 500ms

                    // 🌟 1. 计算虚拟打击时间点列表
                    List<double> virtualHitTimes = new List<double>();

                    if (currentKind == NoteKind.Hold)
                    {
                        // 如果是 Hold，计算尾部的时间
                        double endTimeSeconds = TimeTickConverter.TickToTime(note.HitTime + note.HoldDuration, chart.BpmKeyFrames, chart.InitialBpm);

                        // 套用文档里的公式计算间隔： 30000ms / (BPM * speed) -> 换算成秒就是 30.0 / (BPM * speed)
                        double bpm = chart.InitialBpm <= 0 ? 120.0 : chart.InitialBpm;
                        double speed = Math.Abs(line.InitialSpeed);
                        if (speed < 0.1) speed = 0.1; // 防止除以 0

                        double intervalSeconds = 30.0 / (bpm * speed);
                        if (intervalSeconds < 0.03) intervalSeconds = 0.03; // 性能保护限制：最高 33fps 的爆炸频率

                        // 从头到尾，按照间隔生成连续的打击点
                        for (double t = hitTimeSeconds; t <= endTimeSeconds; t += intervalSeconds)
                        {
                            // 性能优化：只有在距离当前时间 0.5 秒内的特效才需要被渲染！
                            if (currentSeconds - t >= 0 && currentSeconds - t <= fxDurationSeconds)
                            {
                                virtualHitTimes.Add(t);
                            }
                        }
                    }
                    else
                    {
                        // 如果是普通音符，只有一个打击点
                        if (currentSeconds - hitTimeSeconds >= 0 && currentSeconds - hitTimeSeconds <= fxDurationSeconds)
                        {
                            virtualHitTimes.Add(hitTimeSeconds);
                        }
                    }

                    // 🌟 2. 遍历所有激活的打击点并渲染
                    foreach (var vTime in virtualHitTimes)
                    {
                        double elapsedSeconds = currentSeconds - vTime;

                        // 将这个虚拟秒数反推回当时的 Tick，用来查询过去那一瞬间的坐标！
                        int vTick = (int)Math.Round(TimeTickConverter.TimeToTick(vTime, chart.BpmKeyFrames, chart.InitialBpm));

                        // 获取判定线在【这一个虚拟打击瞬间】的历史位置和旋转
                        EasingUtils.CalculateObjectTransform(
                            vTick, chart.KeyFrameEasingDirection,
                            line.AnimatableProperties,
                            out var lineOffset, out var lineScale, out var lineRotationAngle, out _);

                        var linePixelOffset = new Vector(
                            renderInfo.ChartUnitToPixel(lineOffset.X),
                            renderInfo.ChartUnitToPixel(lineOffset.Y));

                        var lineTransform = new TransformGroup()
                        {
                            Children = {
                        new ScaleTransform(lineScale.X, lineScale.Y),
                        new RotateTransform(-lineRotationAngle),
                        new TranslateTransform(linePixelOffset.X, -linePixelOffset.Y),
                    }
                        };

                        // 获取音符在【这一个虚拟打击瞬间】相对判定线的历史偏移
                        EasingUtils.CalculateObjectTransform(
                            vTick, chart.KeyFrameEasingDirection,
                            note.AnimatableProperties,
                            out var noteOffset, out var noteScale, out var noteRotationAngle, out _);

                        var notePixelOffset = new Vector(
                            renderInfo.ChartUnitToPixel(noteOffset.X),
                            renderInfo.ChartUnitToPixel(noteOffset.Y));

                        var noteTransform = new TransformGroup()
                        {
                            Children = {
                        new ScaleTransform(noteScale.X, noteScale.Y),
                        new RotateTransform(noteRotationAngle),
                        new TranslateTransform(notePixelOffset.X, notePixelOffset.Y),
                    }
                        };

                        // ================= 开始绘制 =================
                        drawingContext.PushTransform(lineTransform);
                        drawingContext.PushTransform(noteTransform);

                        double tick = elapsedSeconds / fxDurationSeconds;

                        // === 画主命中特效 ===
                        int frameIndex = (int)Math.Floor(tick * 30);
                        if (frameIndex >= 30) frameIndex = 29;

                        var frame = _hitFxFrames[frameIndex];
                        if (frame != null)
                        {
                            double fxSize = 256.0 * 1.0 * noteScaleRatio * 6.0;
                            drawingContext.PushOpacityMask(new ImageBrush(frame));
                            drawingContext.DrawRectangle(_perfectFxBrush, null, new Rect(-fxSize / 2, -fxSize / 2, fxSize, fxSize));
                            drawingContext.Pop();
                        }

                        // === 画粒子小方块炸开特效 ===
                        // 巧妙的随机种子：用 Note ID 加上当前虚拟时间算 Hash，
                        // 这样既能保证每一次"哒哒哒"爆出来的粒子轨迹不一样，又能保证拖拽进度条时画面不闪烁！
                        Random rand = new Random((note.ID + vTime.ToString("F3")).GetHashCode());

                        for (int p = 0; p < 4; p++)
                        {
                            double j0 = rand.NextDouble() * (265 - 185) + 185;
                            double angle = rand.NextDouble() * Math.PI * 2;
                            double pRot = enableParticleRotation ? rand.NextDouble() * 90 : 0;

                            double ds = j0 * (9 * tick) / (8 * tick + 1);
                            double r3 = 30 * (((0.2078 * tick - 1.6524) * tick + 1.6399) * tick + 0.4988);

                            double particleSize = r3 * noteScaleRatio * 6.0;
                            double particleAlpha = 1.0 - tick;

                            double px = Math.Cos(angle) * ds * noteScaleRatio * 6.0;
                            double py = Math.Sin(angle) * ds * noteScaleRatio * 6.0;

                            byte alphaByte = (byte)Math.Clamp(particleAlpha * 255, 0, 255);
                            var particleBrush = new SolidColorBrush(Color.FromArgb(alphaByte, 255, 236, 160));

                            drawingContext.PushTransform(new TranslateTransform(px, py));
                            drawingContext.PushTransform(new RotateTransform(pRot));

                            drawingContext.DrawRectangle(particleBrush, null, new Rect(-particleSize / 2, -particleSize / 2, particleSize, particleSize));

                            drawingContext.Pop();
                            drawingContext.Pop();
                        }

                        drawingContext.Pop(); // noteTransform
                        drawingContext.Pop(); // lineTransform
                    }
                }
            }
        }
    }
}
