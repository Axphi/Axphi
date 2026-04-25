using Axphi.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Axphi.Components
{
    public class TimelineRuler : FrameworkElement
    {
        // === 1. 时间轴缩放参数 ===
        public static readonly DependencyProperty PixelPerTickProperty =
            DependencyProperty.Register("PixelPerTick", typeof(double), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public double PixelPerTick
        {
            get => (double)GetValue(PixelPerTickProperty);
            set => SetValue(PixelPerTickProperty, value);
        }

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        // === 2. 接收总长度 (可选，如果以后需要控制最长能滚到哪) ===
        public static readonly DependencyProperty TotalTicksProperty =
            DependencyProperty.Register("TotalTicks", typeof(int), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(100000, FrameworkPropertyMetadataOptions.AffectsRender));

        public int TotalTicks
        {
            get => (int)GetValue(TotalTicksProperty);
            set => SetValue(TotalTicksProperty, value);
        }

        // === 3. 视口同步属性 (绑到相机的 X 轴) ===
        public static readonly DependencyProperty VisibleOffsetXProperty =
            DependencyProperty.Register("VisibleOffsetX", typeof(double), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double VisibleOffsetX
        {
            get => (double)GetValue(VisibleOffsetXProperty);
            set => SetValue(VisibleOffsetXProperty, value);
        }

        // === 4. 核心绘画引擎 (OnRender) ===
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // 铺设透明垫板拦截鼠标 (以后如果想做在标尺上拖拽修改时间，这个很有用)
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // 🌟 核心计算：现在的每 Tick 物理像素，完全由这两个属性决定
            double actualPixelsPerTick = PixelPerTick * Zoom;
            if (actualPixelsPerTick <= 0) return;

            // 设定一个最小视觉间距，如果线挤得太密（小于 8 像素），就不画了
            double minSpacing = 8.0;

            // ======== 动态 LOD (视距剔除) 算法 ========
            int step = 1;
            if (actualPixelsPerTick * step < minSpacing) step = 2;
            if (actualPixelsPerTick * step < minSpacing) step = 4;
            if (actualPixelsPerTick * step < minSpacing) step = 8;
            if (actualPixelsPerTick * step < minSpacing) step = 16;
            if (actualPixelsPerTick * step < minSpacing) step = 32;
            if (actualPixelsPerTick * step < minSpacing) step = 128;

            // 冻结画笔与复用字体以提升性能
            Pen measurePen = new Pen(Brushes.White, 1.5); measurePen.Freeze();
            Pen beatPen = new Pen(Brushes.LightGray, 1.0); beatPen.Freeze();
            Pen subPen = new Pen(Brushes.DimGray, 1.0); subPen.Freeze();
            Typeface typeface = new Typeface("Consolas");
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // ================== 【核心：视口剔除算法】 ==================
            // 将屏幕的可见像素范围，反推回 Tick 的范围
            int startTick = (int)(VisibleOffsetX / actualPixelsPerTick) - step;
            if (startTick < 0) startTick = 0;
            startTick = (startTick / step) * step;

            // 终点 Tick 由当前控件的 ActualWidth 决定
            int endTick = (int)((VisibleOffsetX + ActualWidth) / actualPixelsPerTick) + step;
            if (endTick > TotalTicks) endTick = TotalTicks;

            // 把整个画板往左平移 VisibleOffsetX
            dc.PushTransform(new TranslateTransform(-VisibleOffsetX, 0));

            for (int i = startTick; i <= endTick; i += step)
            {
                double x = i * actualPixelsPerTick;

                if (i % 128 == 0)
                {
                    dc.DrawLine(measurePen, new Point(x, 0), new Point(x, 24));
                    FormattedText text = new FormattedText(
                        (i / 128).ToString(), CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, 11, Brushes.White, dpi);
                    dc.DrawText(text, new Point(x + 3, 2));
                }
                else if (i % 32 == 0)
                {
                    dc.DrawLine(beatPen, new Point(x, 12), new Point(x, 24));
                }
                else if (i % 8 == 0)
                {
                    dc.DrawLine(subPen, new Point(x, 18), new Point(x, 24));
                }
                else
                {
                    dc.DrawLine(subPen, new Point(x, 20), new Point(x, 24));
                }
            }

            // 画底部的基准横线
            dc.DrawLine(subPen, new Point(VisibleOffsetX, 24), new Point(VisibleOffsetX + ActualWidth, 24));
            dc.Pop();
        }




        // === 🌟 游标点击与滑动 (Scrubbing) 逻辑 ===

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // 1. 强制捕获鼠标焦点：这样哪怕你按住鼠标滑到了标尺外面，它依然能接收移动事件
            CaptureMouse();

            // 2. 瞬间对齐
            SeekPlayheadToMouse(e.GetPosition(this).X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 如果鼠标被我们捕获了（说明正按着左键拖动），就实时更新游标！
            if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                SeekPlayheadToMouse(e.GetPosition(this).X);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            // 松开鼠标，释放捕获
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }

        // 核心反推算法
        private void SeekPlayheadToMouse(double mouseX)
        {
            if (DataContext is TimeLineViewModel vm)
            {
                // 1. 计算鼠标相当于 0 刻度的绝对物理像素
                // 因为相机可能往右滚了 (ViewportLocation.X 为正)
                // 也可能处于初始的 Padding 留白状态 (ViewportLocation.X 为 -5)
                double totalAbsolutePixels = mouseX + vm.ViewportLocation.X;

                // 2. 当前每 Tick 的实际宽度
                double actualPixelsPerTick = vm.PixelPerTick * vm.Zoom;

                // 3. 像素 ÷ 单价 = 数量，并采用完美的四舍五入吸附手感
                int targetTick = (int)Math.Round(totalAbsolutePixels / actualPixelsPerTick, MidpointRounding.AwayFromZero);

                // 4. 修改 VM 底层数据，钳制在 0 以上
                vm.CurrentTick = Math.Max(0, targetTick);
            }
        }
    }
}