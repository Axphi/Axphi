using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Axphi.Components
{
    [Flags]
    public enum OverlayUiFlags
    {
        None = 0,
        JudgementLineAnchors = 1 << 0,
        NoteCenters = 1 << 1,
        All = JudgementLineAnchors,
    }

    public class ChartRenderer : FrameworkElement
    {
        private static SolidColorBrush _lineYellow = new SolidColorBrush(Color.FromRgb(254, 255, 169)); // #feffa9
        private static SolidColorBrush _lineBlue = new SolidColorBrush(Color.FromRgb(162, 238, 255));   // #a2eeff
        private static SolidColorBrush _lineWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));  // #ffffff

        private static SolidColorBrush _progressWhite = new SolidColorBrush(Color.FromRgb(145, 145, 145));
        private static SolidColorBrush _progressHeadWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));



        // 已弃用
        private static SolidColorBrush _noteFlick = new SolidColorBrush(Color.FromRgb(255, 95, 95));
        private static SolidColorBrush _noteTap = new SolidColorBrush(Color.FromRgb(82, 133, 243));
        private static SolidColorBrush _noteDrag = new SolidColorBrush(Color.FromRgb(255, 222, 145));
        private static SolidColorBrush _noteHold = new SolidColorBrush(Color.FromRgb(81, 180, 255));



        private readonly SolidColorBrush _backgroundDimBrush = new SolidColorBrush(Color.FromArgb(77, 0, 0, 0));

        private const double BaseVerticalFlowPixelsPerSecondAt1080 = 648.0;

        private byte[]? _cachedIllustrationBytes;
        private BitmapSource? _cachedBlurredIllustration;



        // 🌟 1. 静态缓存音符图片 (路径里的 pack://application:,,,/ 是 WPF 的标准资源路径写法)
        // 请把 Resources/Notes/... 换成你实际的文件夹和文件名！
        private static readonly BitmapImage _imgTap = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/tap.png"));
        private static readonly BitmapImage _imgTapMultiHit = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/tapHL.png"));
        private static readonly BitmapImage _imgDrag = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/drag.png"));
        private static readonly BitmapImage _imgDragMultiHit = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/dragHL.png"));
        private static readonly BitmapImage _imgHoldHead = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hold.png"));
        private static readonly BitmapImage _imgHoldMultiHit = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/holdHL.png"));
        private static readonly BitmapImage _imgFlick = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/flick.png"));
        private static readonly BitmapImage _imgFlickMultiHit = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/flickHL.png"));
        private static readonly BitmapImage _imgAnchor = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/UI/anchor.png"));


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
        private static readonly ImageBrush _brushHoldTailMultiHit;
        private static readonly ImageBrush _brushHoldBodyMultiHit;
        private static readonly ImageBrush _brushHoldHeadMultiHit;
        private static readonly ImageBrush _brushHoldTopPaddingMultiHit;
        private static readonly ImageBrush _brushHoldBottomGlowMultiHit;
        private const double BaseNoteTextureWidthPixels = 989.0;
        private const double HoldHeadPixels = 50.0;
        private const double HoldTailPixels = 50.0;
        private const double HoldHighlightTopPaddingPixels = 48.0;
        private const double HoldHighlightTailPixels = 50.0;
        private const double HoldHighlightHeadPixels = 50.0;
        private const double HoldHighlightBottomGlowPixels = 49.0;
        private const double HoldSliceInsetPixels = 0.5;


        // 3. 用静态构造函数初始化切图
        static ChartRenderer()
        {
            var imgHold = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hold.png", UriKind.Absolute));

            double holdHeight = Math.Max(1.0, imgHold.PixelHeight);
            double holdBodyPixels = Math.Max(1.0, holdHeight - HoldHeadPixels - HoldTailPixels);
            _brushHoldHead = CreateHoldSliceBrush(imgHold, holdHeight - HoldHeadPixels, HoldHeadPixels);
            _brushHoldBody = CreateHoldSliceBrush(imgHold, HoldTailPixels, holdBodyPixels);
            _brushHoldTail = CreateHoldSliceBrush(imgHold, 0, HoldTailPixels);

            // 冻结画刷以获得极限渲染性能
            _brushHoldTail.Freeze();
            _brushHoldBody.Freeze();
            _brushHoldHead.Freeze();

            double mhHeight = Math.Max(1.0, _imgHoldMultiHit.PixelHeight);
            double mhBodyPixels = Math.Max(1.0, mhHeight - HoldHighlightTopPaddingPixels - HoldHighlightTailPixels - HoldHighlightHeadPixels - HoldHighlightBottomGlowPixels);
            double mhTailY = HoldHighlightTopPaddingPixels;
            double mhBodyY = mhTailY + HoldHighlightTailPixels;
            double mhHeadY = mhBodyY + mhBodyPixels;
            double mhGlowY = mhHeadY + HoldHighlightHeadPixels;
            _brushHoldTopPaddingMultiHit = CreateHoldSliceBrush(_imgHoldMultiHit, 0, HoldHighlightTopPaddingPixels);
            _brushHoldTailMultiHit = CreateHoldSliceBrush(_imgHoldMultiHit, mhTailY, HoldHighlightTailPixels);
            _brushHoldHeadMultiHit = CreateHoldSliceBrush(_imgHoldMultiHit, mhHeadY, HoldHighlightHeadPixels);
            _brushHoldBodyMultiHit = CreateHoldSliceBrush(_imgHoldMultiHit, mhBodyY, mhBodyPixels);
            _brushHoldBottomGlowMultiHit = CreateHoldSliceBrush(_imgHoldMultiHit, mhGlowY, HoldHighlightBottomGlowPixels);
            _brushHoldTopPaddingMultiHit.Freeze();
            _brushHoldHeadMultiHit.Freeze();
            _brushHoldBodyMultiHit.Freeze();
            _brushHoldTailMultiHit.Freeze();
            _brushHoldBottomGlowMultiHit.Freeze();

            if (_imgAnchor.CanFreeze)
            {
                _imgAnchor.Freeze();
            }

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

        public OverlayUiFlags OverlayUiVisibility
        {
            get { return (OverlayUiFlags)GetValue(OverlayUiVisibilityProperty); }
            set { SetValue(OverlayUiVisibilityProperty, value); }
        }

        public bool ShowAuxiliaryUi
        {
            get { return (bool)GetValue(ShowAuxiliaryUiProperty); }
            set { SetValue(ShowAuxiliaryUiProperty, value); }
        }

        public bool ShowNoteCenters
        {
            get { return (bool)GetValue(ShowNoteCentersProperty); }
            set { SetValue(ShowNoteCentersProperty, value); }
        }

        public int BpmLinesDivisor
        {
            get { return (int)GetValue(BpmLinesDivisorProperty); }
            set { SetValue(BpmLinesDivisorProperty, value); }
        }

        public double BackgroundDimOpacity
        {
            get => (double)GetValue(BackgroundDimOpacityProperty);
            set => SetValue(BackgroundDimOpacityProperty, value);
        }

        public byte[]? IllustrationBytes
        {
            get => (byte[]?)GetValue(IllustrationBytesProperty);
            set => SetValue(IllustrationBytesProperty, value);
        }

        public static readonly DependencyProperty BpmLinesDivisorProperty =
            DependencyProperty.Register("BpmLinesDivisor", typeof(int), typeof(ChartRenderer), new PropertyMetadata(1));

        public static readonly DependencyProperty IllustrationBytesProperty =
            DependencyProperty.Register(
                nameof(IllustrationBytes),
                typeof(byte[]),
                typeof(ChartRenderer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnIllustrationBytesChanged));

        public static readonly DependencyProperty BackgroundDimOpacityProperty =
            DependencyProperty.Register(
                nameof(BackgroundDimOpacity),
                typeof(double),
                typeof(ChartRenderer),
                new FrameworkPropertyMetadata(0.3, FrameworkPropertyMetadataOptions.AffectsRender, OnBackgroundDimOpacityChanged));

        public static readonly DependencyProperty ShowBpmLinesProperty =
            DependencyProperty.Register("ShowBpmLines", typeof(bool), typeof(ChartRenderer), new PropertyMetadata(false));

        public static readonly DependencyProperty OverlayUiVisibilityProperty =
            DependencyProperty.Register(
                nameof(OverlayUiVisibility),
                typeof(OverlayUiFlags),
                typeof(ChartRenderer),
                new FrameworkPropertyMetadata(OverlayUiFlags.All, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowAuxiliaryUiProperty =
            DependencyProperty.Register(
                nameof(ShowAuxiliaryUi),
                typeof(bool),
                typeof(ChartRenderer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowNoteCentersProperty =
            DependencyProperty.Register(
                nameof(ShowNoteCenters),
                typeof(bool),
                typeof(ChartRenderer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        private static void OnIllustrationBytesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ChartRenderer renderer)
            {
                return;
            }

            renderer._cachedIllustrationBytes = null;
            renderer._cachedBlurredIllustration = null;
        }

        private static void OnBackgroundDimOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ChartRenderer renderer)
            {
                return;
            }

            double opacity = Math.Clamp(renderer.BackgroundDimOpacity, 0.0, 1.0);
            if (Math.Abs(opacity - renderer.BackgroundDimOpacity) > 0.000001)
            {
                renderer.BackgroundDimOpacity = opacity;
                return;
            }

            byte alpha = (byte)Math.Round(opacity * 255.0, MidpointRounding.AwayFromZero);
            renderer._backgroundDimBrush.Color = Color.FromArgb(alpha, 0, 0, 0);
        }





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

            Rect viewportRect = new Rect(0, 0, ActualWidth, ActualHeight);
            drawingContext.DrawRectangle(Brushes.Black, null, viewportRect);

            var renderInfo = CalculateRenderInfo();
            RenderBlurredIllustrationBackground(drawingContext, renderInfo);

            if (Chart is not { } chart ||
                chart.JudgementLines is not { } judgementLines)
            {
                return;
            }

            //var time = Time;
            //var renderInfo = CalculateRenderInfo();
            // 1. 获取物理现实时间
            var realTime = Time;

            // 2. 将现实时间转换为底层逻辑运算用的精确 Tick，避免低 BPM 时画面被整数 Tick 量化成低帧率
            double currentTick = CalculateCurrentTick(realTime, chart);
            int currentDiscreteTick = (int)Math.Round(currentTick, MidpointRounding.AwayFromZero);



            //if (time > chart.Duration)
            //{
            //    time = chart.Duration;
            //}

            //RenderProgress(drawingContext, renderInfo, time / chart.Duration);

            // 4. 进度条计算，注意要强制转换为 double，防止整数除法结果为 0
            double progress = chart.Duration == 0 ? 0 : currentTick / chart.Duration;
            RenderProgress(drawingContext, renderInfo, progress);

            var transformToCenter = new TranslateTransform(renderInfo.CanvasWidth / 2, renderInfo.CanvasHeight / 2);
            drawingContext.PushTransform(transformToCenter);

            // ================= 🌟 核心修改：双Pass分层渲染引擎 =================

            // 【第一遍遍历】：只画所有的判定线本身（铺在最底层）
            OverlayUiFlags effectiveOverlayFlags = ShowAuxiliaryUi ? OverlayUiVisibility : OverlayUiFlags.None;
            bool effectiveShowNoteCenters = ShowAuxiliaryUi && ShowNoteCenters;
            var lineById = judgementLines
                .Where(line => !string.IsNullOrWhiteSpace(line.ID))
                .GroupBy(line => line.ID)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var judgementLine in judgementLines)
            {
                if (currentTick < judgementLine.StartTick || currentTick > (judgementLine.StartTick + judgementLine.DurationTicks))
                    continue;

                // 传入 true, false (只画线，不画音符)
                RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, currentTick, true, false, effectiveOverlayFlags, effectiveShowNoteCenters, lineById, multiHitTicks: null);
            }

            var multiHitTicks = CollectMultiHitTicks(chart);

            // 【第二遍遍历】：只画所有的音符（盖在所有判定线的上面）
            foreach (var judgementLine in judgementLines)
            {
                if (currentTick < judgementLine.StartTick || currentTick > (judgementLine.StartTick + judgementLine.DurationTicks))
                    continue;

                // 传入 false, true (只画音符，不画线)
                RenderJudgementLine(drawingContext, renderInfo, chart, judgementLine, currentTick, false, true, effectiveOverlayFlags, effectiveShowNoteCenters, lineById, multiHitTicks);
            }
            // =====================================================================


            // ================= 新增：2. 最后在最上层画独立的击打特效 =================
            double currentSeconds = realTime.TotalSeconds;
            RenderHitEffects(drawingContext, renderInfo, chart, currentDiscreteTick, currentSeconds, lineById);
            // =====================================================================


            drawingContext.Pop();
        }

        private void RenderBlurredIllustrationBackground(DrawingContext drawingContext, RenderInfo renderInfo)
        {
            Rect viewportRect = new Rect(0, 0, renderInfo.CanvasWidth, renderInfo.CanvasHeight);
            if (viewportRect.Width <= 0 || viewportRect.Height <= 0)
            {
                return;
            }

            var blurredSource = EnsureBlurredIllustration(IllustrationBytes);
            if (blurredSource == null)
            {
                return;
            }

            Rect chartRect = new Rect(
                (renderInfo.CanvasWidth - renderInfo.ClientWidth) * 0.5,
                (renderInfo.CanvasHeight - renderInfo.ClientHeight) * 0.5,
                renderInfo.ClientWidth,
                renderInfo.ClientHeight);
            Rect drawRect = FitCoverRect(chartRect, blurredSource.Width, blurredSource.Height);

            drawingContext.PushClip(new RectangleGeometry(chartRect));
            drawingContext.DrawImage(blurredSource, drawRect);
            drawingContext.Pop();

            drawingContext.DrawRectangle(_backgroundDimBrush, null, chartRect);
        }

        private static Rect FitCoverRect(Rect container, double sourceWidth, double sourceHeight)
        {
            if (container.Width <= 0 || container.Height <= 0)
            {
                return Rect.Empty;
            }

            double safeWidth = Math.Max(1.0, sourceWidth);
            double safeHeight = Math.Max(1.0, sourceHeight);
            double scale = Math.Max(container.Width / safeWidth, container.Height / safeHeight);
            double drawWidth = safeWidth * scale;
            double drawHeight = safeHeight * scale;

            return new Rect(
                container.X + (container.Width - drawWidth) * 0.5,
                container.Y + (container.Height - drawHeight) * 0.5,
                drawWidth,
                drawHeight);
        }

        private BitmapSource? EnsureBlurredIllustration(byte[]? illustrationBytes)
        {
            if (illustrationBytes is not { Length: > 0 })
            {
                return null;
            }

            if (ReferenceEquals(_cachedIllustrationBytes, illustrationBytes) && _cachedBlurredIllustration is not null)
            {
                return _cachedBlurredIllustration;
            }

            BitmapSource? blurred = null;
            try
            {
                BitmapSource decoded = DecodeBitmap(illustrationBytes);
                BitmapSource normalized = NormalizeBitmap(decoded);
                int width = normalized.PixelWidth;
                int height = normalized.PixelHeight;
                int stride = width * 4;
                byte[] sourcePixels = new byte[stride * height];
                normalized.CopyPixels(sourcePixels, stride, 0);
                byte[] sourceRgb = ExtractRgb(sourcePixels, width, height);
                byte[] blurredRgb = ApplyGaussianBlurRgb(sourceRgb, width, height, 50.0);
                byte[] blurredPixels = PackRgbToBgra(blurredRgb, width, height);

                var output = new WriteableBitmap(width, height, normalized.DpiX, normalized.DpiY, PixelFormats.Bgra32, null);
                output.WritePixels(new Int32Rect(0, 0, width, height), blurredPixels, stride, 0);
                if (output.CanFreeze)
                {
                    output.Freeze();
                }

                blurred = output;
            }
            catch
            {
                blurred = null;
            }

            _cachedIllustrationBytes = illustrationBytes;
            _cachedBlurredIllustration = blurred;
            return blurred;
        }

        private static BitmapSource DecodeBitmap(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static BitmapSource NormalizeBitmap(BitmapSource source)
        {
            BitmapSource formatSource = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            const int maxSide = 1600;
            int width = Math.Max(1, formatSource.PixelWidth);
            int height = Math.Max(1, formatSource.PixelHeight);
            int largest = Math.Max(width, height);
            if (largest <= maxSide)
            {
                return formatSource;
            }

            double scale = maxSide / (double)largest;
            int scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
            int scaledHeight = Math.Max(1, (int)Math.Round(height * scale));
            var scaled = new TransformedBitmap(formatSource, new ScaleTransform(
                scaledWidth / (double)width,
                scaledHeight / (double)height));
            if (scaled.CanFreeze)
            {
                scaled.Freeze();
            }

            return scaled;
        }

        private static byte[] ExtractRgb(byte[] bgra, int width, int height)
        {
            byte[] rgb = new byte[width * height * 3];
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int bgraIndex = i * 4;
                int rgbIndex = i * 3;
                rgb[rgbIndex] = bgra[bgraIndex + 2];
                rgb[rgbIndex + 1] = bgra[bgraIndex + 1];
                rgb[rgbIndex + 2] = bgra[bgraIndex];
            }

            return rgb;
        }

        private static byte[] PackRgbToBgra(byte[] rgb, int width, int height)
        {
            byte[] bgra = new byte[width * height * 4];
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int bgraIndex = i * 4;
                int rgbIndex = i * 3;
                bgra[bgraIndex] = rgb[rgbIndex + 2];
                bgra[bgraIndex + 1] = rgb[rgbIndex + 1];
                bgra[bgraIndex + 2] = rgb[rgbIndex];
                bgra[bgraIndex + 3] = 255;
            }

            return bgra;
        }

        private static byte[] ApplyGaussianBlurRgb(byte[] sourceRgb, int width, int height, double sigma)
        {
            if (sigma <= 0)
            {
                return (byte[])sourceRgb.Clone();
            }

            int[] boxSizes = GetGaussianBoxSizes(sigma, 3);
            byte[] src = (byte[])sourceRgb.Clone();
            byte[] tmp = new byte[src.Length];
            byte[] dst = new byte[src.Length];

            foreach (int boxSize in boxSizes)
            {
                int radius = Math.Max(0, (boxSize - 1) / 2);
                if (radius == 0)
                {
                    continue;
                }

                BoxBlurHorizontalRgb(src, tmp, width, height, radius);
                BoxBlurVerticalRgb(tmp, dst, width, height, radius);

                byte[] swap = src;
                src = dst;
                dst = swap;
            }

            return src;
        }

        private static int[] GetGaussianBoxSizes(double sigma, int boxCount)
        {
            double widthIdeal = Math.Sqrt((12.0 * sigma * sigma / boxCount) + 1.0);
            int lower = (int)Math.Floor(widthIdeal);
            if (lower % 2 == 0)
            {
                lower--;
            }

            int upper = lower + 2;
            double matchIdeal =
                (12.0 * sigma * sigma - (boxCount * lower * lower) - (4.0 * boxCount * lower) - (3.0 * boxCount))
                / ((-4.0 * lower) - 4.0);
            int lowerCount = (int)Math.Round(matchIdeal);
            lowerCount = Math.Clamp(lowerCount, 0, boxCount);

            int[] sizes = new int[boxCount];
            for (int i = 0; i < boxCount; i++)
            {
                sizes[i] = i < lowerCount ? lower : upper;
            }

            return sizes;
        }

        private static void BoxBlurHorizontalRgb(byte[] src, byte[] dst, int width, int height, int radius)
        {
            int windowSize = radius * 2 + 1;

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                int sumR = 0, sumG = 0, sumB = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int sx = Math.Clamp(k, 0, width - 1);
                    int srcIndex = (rowStart + sx) * 3;
                    sumR += src[srcIndex];
                    sumG += src[srcIndex + 1];
                    sumB += src[srcIndex + 2];
                }

                for (int x = 0; x < width; x++)
                {
                    int outIndex = (rowStart + x) * 3;
                    dst[outIndex] = (byte)(sumR / windowSize);
                    dst[outIndex + 1] = (byte)(sumG / windowSize);
                    dst[outIndex + 2] = (byte)(sumB / windowSize);

                    int removeX = Math.Clamp(x - radius, 0, width - 1);
                    int addX = Math.Clamp(x + radius + 1, 0, width - 1);

                    int removeIndex = (rowStart + removeX) * 3;
                    int addIndex = (rowStart + addX) * 3;

                    sumR += src[addIndex] - src[removeIndex];
                    sumG += src[addIndex + 1] - src[removeIndex + 1];
                    sumB += src[addIndex + 2] - src[removeIndex + 2];
                }
            }
        }

        private static void BoxBlurVerticalRgb(byte[] src, byte[] dst, int width, int height, int radius)
        {
            int windowSize = radius * 2 + 1;
            for (int x = 0; x < width; x++)
            {
                int sumR = 0, sumG = 0, sumB = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int sy = Math.Clamp(k, 0, height - 1);
                    int srcIndex = (sy * width + x) * 3;
                    sumR += src[srcIndex];
                    sumG += src[srcIndex + 1];
                    sumB += src[srcIndex + 2];
                }

                for (int y = 0; y < height; y++)
                {
                    int outIndex = (y * width + x) * 3;
                    dst[outIndex] = (byte)(sumR / windowSize);
                    dst[outIndex + 1] = (byte)(sumG / windowSize);
                    dst[outIndex + 2] = (byte)(sumB / windowSize);

                    int removeY = Math.Clamp(y - radius, 0, height - 1);
                    int addY = Math.Clamp(y + radius + 1, 0, height - 1);
                    int removeIndex = (removeY * width + x) * 3;
                    int addIndex = (addY * width + x) * 3;

                    sumR += src[addIndex] - src[removeIndex];
                    sumG += src[addIndex + 1] - src[removeIndex + 1];
                    sumB += src[addIndex + 2] - src[removeIndex + 2];
                }
            }
        }

        /// <summary>
        /// 将播放器的 TimeSpan 物理时间实时换算为 谱面系统的 Tick (128分音符数量)
        /// </summary>
        private static double CalculateCurrentTick(TimeSpan realTime, Chart chart)
        {
            return TimeTickConverter.TimeToTick(realTime.TotalSeconds, chart.BpmKeyFrames, chart.InitialBpm);
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
        private static double GetBaseVerticalFlowPixelsPerSecond(RenderInfo renderInfo)
        {
            return BaseVerticalFlowPixelsPerSecondAt1080 * (renderInfo.ClientHeight / 1080.0);
        }

        private static double CalculateIntegralDistance(double startTick, double endTick, JudgementLine line, Chart chart, double noteSpeedMultiplier, RenderInfo renderInfo)
        {
            if (Math.Abs(startTick - endTick) < double.Epsilon) return 0;

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

            double tMin = Math.Min(startTick, endTick);
            double tMax = Math.Max(startTick, endTick);

            double stepTick = (double)(tMax - tMin) / steps;

            for (int i = 0; i < steps; i++)
            {
                // 1. 切割出极小的时间片段
                double t1 = tMin + i * stepTick;
                double t2 = tMin + (i + 1) * stepTick;

                double sec1 = TimeTickConverter.TickToTime(t1, chart.BpmKeyFrames, chart.InitialBpm);
                double sec2 = TimeTickConverter.TickToTime(t2, chart.BpmKeyFrames, chart.InitialBpm);

                // 2. 抓取这个时间片段【正中间】的那一瞬间的速度（直接白嫖你写好的神兽方法）
                double midTick = (t1 + t2) / 2.0;
                EasingUtils.CalculateObjectSingleTransform(
                    midTick,
                    chart.KeyFrameEasingDirection,
                    line.Properties.Speed.InitialValue,
                    line.Properties.Speed.KeyFrames,
                    Axphi.Utilities.MathUtils.Lerp,
                    line.Properties.Speed.ExpressionEnabled,
                    line.Properties.Speed.ExpressionText,
                    chart,
                    out var midSpeed);

                // 3. 微小距离 = 瞬间速度 × 微小时间，然后累加！
                totalDistance += midSpeed * (sec2 - sec1);
            }

            double baseVerticalFlowPixelsPerSecond = GetBaseVerticalFlowPixelsPerSecond(renderInfo);
            double pixelDistance = totalDistance * baseVerticalFlowPixelsPerSecond * noteSpeedMultiplier;

            return startTick <= endTick ? pixelDistance : -pixelDistance;
        }
        private static void RenderJudgementLine(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, JudgementLine line, double currentTick, bool drawLine, bool drawNotes, OverlayUiFlags overlayUiVisibility, bool showNoteCenters, IReadOnlyDictionary<string, JudgementLine> lineById, HashSet<int>? multiHitTicks)
        {
            Matrix transformMatrix = BuildLineWorldMatrix(line, currentTick, renderInfo, chart, lineById, []);
            var transform = new MatrixTransform(transformMatrix);

            EasingUtils.CalculateObjectTransform(
                currentTick, chart.KeyFrameEasingDirection,
                line.Properties,
                chart,
                line,
                out var anchor, out _, out _, out _, out var opacity);

            var pixelAnchor = new Vector(
                renderInfo.ChartUnitToPixel(anchor.X),
                -renderInfo.ChartUnitToPixel(anchor.Y));

            drawingContext.PushTransform(transform);

            // ================= 🌟 分离逻辑 1：绘制判定线本体 =================
            if (drawLine)
            {
                drawingContext.PushOpacity(opacity / 100);

                double lineLength = renderInfo.CanvasHeight * 5.76;
                double thickness = renderInfo.CanvasHeight * 0.0075;

                drawingContext.DrawRectangle(_lineYellow, null, new Rect(-lineLength / 2, -thickness / 2, lineLength, thickness));

                if ((overlayUiVisibility & OverlayUiFlags.JudgementLineAnchors) != 0)
                {
                    double anchorWidth = renderInfo.ChartUnitToPixel(0.66);
                    double anchorHeight = anchorWidth * (_imgAnchor.PixelHeight / (double)Math.Max(1, _imgAnchor.PixelWidth));
                    drawingContext.DrawImage(_imgAnchor, new Rect(pixelAnchor.X - anchorWidth / 2.0, pixelAnchor.Y - anchorHeight / 2.0, anchorWidth, anchorHeight));
                }

                drawingContext.Pop(); // note 不继承 line 的 opacity
            }
            // ================= 🌟 分离逻辑 2：绘制附着的音符 =================
            if (drawNotes && line.Notes is { } notes)
            {
                // speed 默认: 1, 渲染器宽 16 , 高 9
                // ================= 🌟 核心修改：音符渲染层级 (Z-Index) =================
                // 画家算法：先画的在底层，后画的在顶层。
                // 我们希望的顶层显示顺序是 Drag > Flick > Tap > Hold
                // 所以渲染顺序必须是 Hold(0) -> Tap(1) -> Flick(2) -> Drag(3)
                int currentDiscreteTick = (int)Math.Round(currentTick, MidpointRounding.AwayFromZero);
                var sortedNotes = notes.OrderBy(note =>
                {
                    var currentKind = KeyFrameUtils.GetStepValueAtTick(note.Properties.Kind.KeyFrames, currentDiscreteTick, note.Properties.Kind.InitialValue);
                    return currentKind switch
                    {
                        NoteKind.Hold => 0,
                        NoteKind.Tap => 1,
                        NoteKind.Flick => 2,
                        NoteKind.Drag => 3,
                        _ => 0
                    };
                });

                foreach (var note in sortedNotes)
                {
                    RenderNote(drawingContext, renderInfo, chart, note, currentTick, line, showNoteCenters, multiHitTicks?.Contains(note.HitTime) == true);
                }
            }


            drawingContext.Pop();
        }

        private static Matrix BuildLineWorldMatrix(
            JudgementLine line,
            double currentTick,
            RenderInfo renderInfo,
            Chart chart,
            IReadOnlyDictionary<string, JudgementLine> lineById,
            HashSet<string> visiting)
        {
            Matrix localMatrix = BuildLineLocalMatrix(line, currentTick, renderInfo, chart);

            if (string.IsNullOrWhiteSpace(line.ParentLineId) ||
                line.ParentLineId == line.ID ||
                !visiting.Add(line.ID))
            {
                return localMatrix;
            }

            if (!lineById.TryGetValue(line.ParentLineId, out var parentLine))
            {
                visiting.Remove(line.ID);
                return localMatrix;
            }

            Matrix parentMatrix = BuildLineWorldMatrix(parentLine, currentTick, renderInfo, chart, lineById, visiting);
            localMatrix.Append(parentMatrix);

            visiting.Remove(line.ID);
            return localMatrix;
        }

        private static Matrix BuildLineLocalMatrix(JudgementLine line, double currentTick, RenderInfo renderInfo, Chart chart)
        {
            EasingUtils.CalculateObjectTransform(
                currentTick, chart.KeyFrameEasingDirection,
                line.Properties,
                chart,
                line,
                out var anchor, out var offset, out var scale, out var rotationAngle, out _);

            var pixelOffset = new Vector(
                renderInfo.ChartUnitToPixel(offset.X),
                renderInfo.ChartUnitToPixel(offset.Y));
            var pixelAnchor = new Vector(
                renderInfo.ChartUnitToPixel(anchor.X),
                -renderInfo.ChartUnitToPixel(anchor.Y));

            var localTransform = new TransformGroup()
            {
                Children =
                {
                    new TranslateTransform(-pixelAnchor.X, -pixelAnchor.Y),
                    new ScaleTransform(scale.X, scale.Y),
                    new RotateTransform(-rotationAngle),
                    new TranslateTransform(pixelAnchor.X, pixelAnchor.Y),
                    new TranslateTransform(pixelOffset.X, -pixelOffset.Y),
                }
            };

            return localTransform.Value;
        }

        private static void RenderNote(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, Note note, double currentTick, JudgementLine line, bool showNoteCenter, bool isMultiHit)
        {
            int currentDiscreteTick = (int)Math.Round(currentTick, MidpointRounding.AwayFromZero);
            var ticksFromNow = note.HitTime - currentTick;

            // 绝对值 > 1000 Tick 就不渲染，优化性能
            if (Math.Abs(ticksFromNow) > 1000) return;

            // ================= 1. 提前提取音符类型 =================
            var currentKind = KeyFrameUtils.GetStepValueAtTick(note.Properties.Kind.KeyFrames, currentDiscreteTick, note.Properties.Kind.InitialValue);

            // ================= 2. 视觉消失引擎 =================
            // Tap, Drag, Flick：只要越过判定线，直接消失！
            if (currentKind != NoteKind.Hold && currentTick >= note.HitTime) return;

            // Hold：只有当整条尾巴都彻底越过判定线时，才完全消失！
            if (currentKind == NoteKind.Hold && currentTick >= note.HitTime + note.HoldDuration) return;

            // ================= 3. 计算物理下落距离 (保持真实下落，绝不定住！) =================
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            double hitTimeSeconds = TimeTickConverter.TickToTime(note.HitTime, chart.BpmKeyFrames, chart.InitialBpm);

            // 这里有问题, noteSpeed 应该是一个关键帧集合而非定值
            double noteSpeedMultiplier =  note.Properties.Speed.InitialValue;

            double baseVerticalFlowPixelsPerSecond = GetBaseVerticalFlowPixelsPerSecond(renderInfo);

            double currentRealtimeSpeed = line.Properties.Speed.InitialValue;
            if (line.SpeedMode == "Realtime")
            {
                EasingUtils.CalculateObjectSingleTransform(
                    currentTick, chart.KeyFrameEasingDirection,
                    line.Properties.Speed.InitialValue,     
                    line.Properties.Speed.KeyFrames,         
                    Axphi.Utilities.MathUtils.Lerp,
                    line.Properties.Speed.ExpressionEnabled, 
                    line.Properties.Speed.ExpressionText,
                    chart,
                    line,
                    out currentRealtimeSpeed);
            }

            double pixelDistance = 0;
            if (line.SpeedMode == "Realtime")
            {
                double actualPixelsPerSecond = baseVerticalFlowPixelsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier;
                pixelDistance = actualPixelsPerSecond * (hitTimeSeconds - currentSeconds);
            }
            else
            {
                pixelDistance = CalculateIntegralDistance(currentTick, note.HitTime, line, chart, noteSpeedMultiplier, renderInfo);
            }

            // 距离取反（屏幕上方是负方向）
            pixelDistance = -pixelDistance;

            // 计算音符本体的变换和透明度
            EasingUtils.CalculateObjectTransform(
                currentTick, chart.KeyFrameEasingDirection,
                note.Properties,
                out var anchor, out var offset, out var scale, out var rotationAngle, out var opacity);

            var pixelOffset = new Vector(renderInfo.ChartUnitToPixel(offset.X), renderInfo.ChartUnitToPixel(offset.Y));
            var pixelAnchor = new Vector(renderInfo.ChartUnitToPixel(anchor.X), renderInfo.ChartUnitToPixel(anchor.Y));

            var noteTransform = new TransformGroup()
            {
                Children = {
            new TranslateTransform(-pixelAnchor.X, -pixelAnchor.Y),
            new ScaleTransform(scale.X, scale.Y),
            new RotateTransform(rotationAngle),
            new TranslateTransform(pixelAnchor.X, pixelAnchor.Y),
            new TranslateTransform(pixelOffset.X, pixelOffset.Y + pixelDistance),
        }
            };

            // ================= 🌟 魔法点 1：施加隐形裁切蒙版 (Clip) 🌟 =================
            bool isHold = currentKind == NoteKind.Hold;
            bool useMultiHitResource = isMultiHit;

            if (isHold)
            {
                var clipGeometry = new RectangleGeometry(new Rect(-100000, -100000, 200000, 100000));
                drawingContext.PushClip(clipGeometry);
            }
            // =========================================================================

            drawingContext.PushTransform(noteTransform);
            drawingContext.PushOpacity(opacity / 100);

            // ================= 5. 开始画图 =================
            if (currentKind == NoteKind.Hold)
            {
                double endTimeSeconds = TimeTickConverter.TickToTime(note.HitTime + note.HoldDuration, chart.BpmKeyFrames, chart.InitialBpm);
                double holdDistance = 0;

                if (line.SpeedMode == "Realtime")
                {
                    double actualPixelsPerSecond = baseVerticalFlowPixelsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier;
                    holdDistance = actualPixelsPerSecond * (endTimeSeconds - hitTimeSeconds);
                }
                else
                {
                    holdDistance = CalculateIntegralDistance(note.HitTime, note.HitTime + note.HoldDuration, line, chart, noteSpeedMultiplier, renderInfo);
                }

                bool isFlipped = holdDistance < 0;
                double holdPixelLength = Math.Abs(holdDistance);

                double notePixelWidth = GetRenderedNotePixelWidth(renderInfo.ChartUnitToPixel(1.95), currentKind, useMultiHitResource);
                double sourceWidth = GetTexturePixelWidth(currentKind, useMultiHitResource);
                double topPaddingPixels = useMultiHitResource ? HoldHighlightTopPaddingPixels : 0.0;
                double headPixels = useMultiHitResource ? HoldHighlightHeadPixels : HoldHeadPixels;
                double tailPixels = useMultiHitResource ? HoldHighlightTailPixels : HoldTailPixels;
                double bottomGlowPixels = useMultiHitResource ? HoldHighlightBottomGlowPixels : 0.0;
                double topPaddingHeight = notePixelWidth * (topPaddingPixels / sourceWidth);
                double headHeight = notePixelWidth * (headPixels / sourceWidth);
                double tailHeight = notePixelWidth * (tailPixels / sourceWidth);
                double bottomGlowHeight = notePixelWidth * (bottomGlowPixels / sourceWidth);
                if (holdPixelLength < headHeight + tailHeight) holdPixelLength = headHeight + tailHeight;

                double bodyHeight = Math.Max(0, holdPixelLength - headHeight - tailHeight);
                Rect headRect = new Rect(-notePixelWidth / 2, -headHeight, notePixelWidth, headHeight);
                Rect bodyRect = new Rect(-notePixelWidth / 2, -(headHeight + bodyHeight), notePixelWidth, bodyHeight);
                Rect tailRect = new Rect(-notePixelWidth / 2, -(headHeight + bodyHeight + tailHeight), notePixelWidth, tailHeight);
                Rect topPaddingRect = new Rect(-notePixelWidth / 2, -(headHeight + bodyHeight + tailHeight + topPaddingHeight), notePixelWidth, topPaddingHeight);
                Rect bottomGlowRect = new Rect(-notePixelWidth / 2, 0, notePixelWidth, bottomGlowHeight);

                if (isFlipped) drawingContext.PushTransform(new ScaleTransform(1, -1));

                if (useMultiHitResource && topPaddingHeight > 0)
                {
                    drawingContext.DrawRectangle(_brushHoldTopPaddingMultiHit, null, topPaddingRect);
                }
                drawingContext.DrawRectangle(useMultiHitResource ? _brushHoldBodyMultiHit : _brushHoldBody, null, bodyRect);
                drawingContext.DrawRectangle(useMultiHitResource ? _brushHoldTailMultiHit : _brushHoldTail, null, tailRect);
                drawingContext.DrawRectangle(useMultiHitResource ? _brushHoldHeadMultiHit : _brushHoldHead, null, headRect);
                if (useMultiHitResource && bottomGlowHeight > 0)
                {
                    drawingContext.DrawRectangle(_brushHoldBottomGlowMultiHit, null, bottomGlowRect);
                }

                if (isFlipped) drawingContext.Pop();
            }
            else
            {
                ImageSource imgSrc = currentKind switch
                {
                    NoteKind.Tap => isMultiHit ? _imgTapMultiHit : _imgTap,
                    NoteKind.Drag => isMultiHit ? _imgDragMultiHit : _imgDrag,
                    NoteKind.Flick => isMultiHit ? _imgFlickMultiHit : _imgFlick,
                    _ => isMultiHit ? _imgTapMultiHit : _imgTap,
                };

                var aspectRatio = imgSrc.Height / imgSrc.Width;
                var notePixelWidth = GetRenderedNotePixelWidth(renderInfo.ChartUnitToPixel(1.95), currentKind, isMultiHit);
                var notePixelHeight = notePixelWidth * aspectRatio;

                drawingContext.DrawImage(imgSrc, new Rect(-notePixelWidth / 2, -notePixelHeight / 2, notePixelWidth, notePixelHeight));
            }

            if (showNoteCenter)
            {
                double anchorWidth = renderInfo.ChartUnitToPixel(0.46);
                double anchorHeight = anchorWidth * (_imgAnchor.PixelHeight / (double)Math.Max(1, _imgAnchor.PixelWidth));
                drawingContext.DrawImage(_imgAnchor, new Rect(-anchorWidth / 2.0, -anchorHeight / 2.0, anchorWidth, anchorHeight));
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

        private static HashSet<int> CollectMultiHitTicks(Chart chart)
        {
            return chart.JudgementLines?
                .Where(line => line.Notes != null)
                .SelectMany(line => line.Notes!)
                .GroupBy(note => note.HitTime)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet() ?? [];
        }

        private static double GetRenderedNotePixelWidth(double basePixelWidth, NoteKind kind, bool isMultiHit)
        {
            return basePixelWidth * (GetTexturePixelWidth(kind, isMultiHit) / BaseNoteTextureWidthPixels);
        }

        private static double GetTexturePixelWidth(NoteKind kind, bool isMultiHit)
        {
            BitmapImage image = kind switch
            {
                NoteKind.Tap => isMultiHit ? _imgTapMultiHit : _imgTap,
                NoteKind.Drag => isMultiHit ? _imgDragMultiHit : _imgDrag,
                NoteKind.Hold => isMultiHit ? _imgHoldMultiHit : _imgHoldHead,
                NoteKind.Flick => isMultiHit ? _imgFlickMultiHit : _imgFlick,
                _ => isMultiHit ? _imgTapMultiHit : _imgTap,
            };

            return Math.Max(1.0, image.PixelWidth);
        }

        private static ImageBrush CreateHoldSliceBrush(BitmapImage image, double yPixels, double heightPixels)
        {
            double totalHeight = Math.Max(1.0, image.PixelHeight);
            double safeHeight = Math.Max(1.0, heightPixels);
            double inset = Math.Min(HoldSliceInsetPixels, Math.Max(0, safeHeight * 0.5 - 0.01));
            double y = Math.Clamp(yPixels + inset, 0, totalHeight - 0.01);
            double h = Math.Clamp(safeHeight - inset * 2.0, 0.01, totalHeight - y);

            return new ImageBrush(image)
            {
                Viewbox = new Rect(0, y / totalHeight, 1, h / totalHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
        }

        private static void RenderHitEffects(DrawingContext drawingContext, RenderInfo renderInfo, Chart chart, int currentTick, double currentSeconds, IReadOnlyDictionary<string, JudgementLine> lineById)
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
                    var currentKind = KeyFrameUtils.GetStepValueAtTick(note.Properties.Kind.KeyFrames, note.HitTime, note.Properties.Kind.InitialValue);
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
                        double speed = Math.Abs(line.Properties.Speed.InitialValue);
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

                        // 将这个虚拟秒数反推回当时的精确 Tick，用于获取命中瞬间的历史变换。
                        double vTick = TimeTickConverter.TimeToTick(vTime, chart.BpmKeyFrames, chart.InitialBpm);

                        // 使用与音符渲染一致的判定线完整变换链，确保旋转后坐标严格一致。
                        Matrix lineWorldMatrix = BuildLineWorldMatrix(line, vTick, renderInfo, chart, lineById, []);

                        // 使用与音符渲染一致的音符完整变换链，确保 offset/anchor/rotation 对命中特效生效。
                        EasingUtils.CalculateObjectTransform(
                            vTick, chart.KeyFrameEasingDirection,
                            note.Properties,
                            out var noteAnchor, out var noteOffset, out var noteScale, out var noteRotationAngle, out _);

                        var notePixelOffset = new Vector(
                            renderInfo.ChartUnitToPixel(noteOffset.X),
                            renderInfo.ChartUnitToPixel(noteOffset.Y));
                        var notePixelAnchor = new Vector(
                            renderInfo.ChartUnitToPixel(noteAnchor.X),
                            renderInfo.ChartUnitToPixel(noteAnchor.Y));

                        var noteTransform = new TransformGroup()
                        {
                            Children =
                            {
                                new TranslateTransform(-notePixelAnchor.X, -notePixelAnchor.Y),
                                new ScaleTransform(noteScale.X, noteScale.Y),
                                new RotateTransform(noteRotationAngle),
                                new TranslateTransform(notePixelAnchor.X, notePixelAnchor.Y),
                                new TranslateTransform(notePixelOffset.X, notePixelOffset.Y),
                            }
                        };

                        // ================= 开始绘制 =================
                        // 命中点跟随历史变换，但特效始终保持屏幕正向（不继承任何旋转）。
                        Point noteLocalHitPoint = noteTransform.Transform(new Point(0, 0));
                        Point fxCenter = lineWorldMatrix.Transform(noteLocalHitPoint);

                        double tick = elapsedSeconds / fxDurationSeconds;

                        // === 画主命中特效 ===
                        int frameIndex = (int)Math.Floor(tick * 30);
                        if (frameIndex >= 30) frameIndex = 29;

                        var frame = _hitFxFrames[frameIndex];
                        if (frame != null)
                        {
                            double fxSize = 256.0 * 1.0 * noteScaleRatio * 6.0;
                            drawingContext.PushOpacityMask(new ImageBrush(frame));
                            drawingContext.DrawRectangle(_perfectFxBrush, null, new Rect(fxCenter.X - fxSize / 2, fxCenter.Y - fxSize / 2, fxSize, fxSize));
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

                            drawingContext.PushTransform(new TranslateTransform(fxCenter.X + px, fxCenter.Y + py));
                            drawingContext.PushTransform(new RotateTransform(pRot));

                            drawingContext.DrawRectangle(particleBrush, null, new Rect(-particleSize / 2, -particleSize / 2, particleSize, particleSize));

                            drawingContext.Pop();
                            drawingContext.Pop();
                        }
                    }
                }
            }
        }
    }
}
