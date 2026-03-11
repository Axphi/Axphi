using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Axphi.Components
{
    public class TimelineRuler : FrameworkElement
    {
        

        // === 2. 接收总长度 (Tick) ===
        public static readonly DependencyProperty TotalTicksProperty =
            DependencyProperty.Register("TotalTicks", typeof(int), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(10000, FrameworkPropertyMetadataOptions.AffectsRender));

        public int TotalTicks
        {
            get => (int)GetValue(TotalTicksProperty);
            set => SetValue(TotalTicksProperty, value);
        }


        // === 2. 【新增】视口同步属性 (用于剔除不可见刻度) ===

        // 记录当前屏幕左侧滚到了多少像素
        public static readonly DependencyProperty VisibleOffsetXProperty =
            DependencyProperty.Register("VisibleOffsetX", typeof(double), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double VisibleOffsetX
        {
            get => (double)GetValue(VisibleOffsetXProperty);
            set => SetValue(VisibleOffsetXProperty, value);
        }

        // 记录当前屏幕本身的物理宽度
        public static readonly DependencyProperty ViewportWidthProperty =
            DependencyProperty.Register("ViewportWidth", typeof(double), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(1000.0, FrameworkPropertyMetadataOptions.AffectsRender));

        // 新增一个依赖属性，用来接收虚拟总宽度
        public static readonly DependencyProperty VirtualTotalWidthProperty =
            DependencyProperty.Register("VirtualTotalWidth", typeof(double), typeof(TimelineRuler),
                new FrameworkPropertyMetadata(1000.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public double VirtualTotalWidth
        {
            get => (double)GetValue(VirtualTotalWidthProperty);
            set => SetValue(VirtualTotalWidthProperty, value);
        }

        public double ViewportWidth
        {
            get => (double)GetValue(ViewportWidthProperty);
            set => SetValue(ViewportWidthProperty, value);
        }


        // === 3. 核心绘画引擎 (OnRender) ===
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // 修改这里的判断条件
            if (TotalTicks <= 0 || VirtualTotalWidth <= 0) return;

            

            // 根据当前宽度和总 Ticks，算出每个 Tick 占多少像素
            // 用虚拟总宽度来计算每个 Tick 的像素大小，而不是 ActualWidth
            double pixelsPerTick = VirtualTotalWidth / TotalTicks;

            // 设定一个最小视觉间距，如果线挤得太密（小于 8 像素），就不画了
            double minSpacing = 8.0;

            // ======== 动态 LOD (视距剔除) 算法 ========
            // 根据缩放比例，决定我们当前最小能容忍多小的音符单位
            // 1个 Tick = 1/128音符
            int step = 1; // 默认画 1/128 刻度
            if (pixelsPerTick * step < minSpacing) step = 2;   // 太密了！改画 1/64
            if (pixelsPerTick * step < minSpacing) step = 4;   // 还密！改画 1/32
            if (pixelsPerTick * step < minSpacing) step = 8;   // 改画 1/16
            if (pixelsPerTick * step < minSpacing) step = 16;  // 改画 1/8
            if (pixelsPerTick * step < minSpacing) step = 32;  // 改画 1/4 (整拍 Beat)
            if (pixelsPerTick * step < minSpacing) step = 128; // 最底线：只画小节线 (Measure)

            // 准备好画笔
            

            // 【优化 1】冻结画笔与复用字体！不要在 OnRender 里重复创建非冻结资源
            Pen measurePen = new Pen(Brushes.White, 1.5); measurePen.Freeze();   // 小节线：白色，粗
            Pen beatPen = new Pen(Brushes.LightGray, 1.0); beatPen.Freeze();     // 拍线：浅灰，正常
            Pen subPen = new Pen(Brushes.DimGray, 1.0); subPen.Freeze();         // 细分线：暗灰，正常
            Typeface typeface = new Typeface("Consolas");
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // ================== 【核心：视口剔除算法】 ==================
            // 将屏幕的可见像素范围，反推回 Tick 的范围
            int startTick = (int)(VisibleOffsetX / pixelsPerTick) - step; // 减一个 step 留点缓冲防闪烁
            if (startTick < 0) startTick = 0;

            // 对齐到当前的刻度步长 (保证滚动时细线不会闪烁跳动)
            startTick = (startTick / step) * step;

            int endTick = (int)((VisibleOffsetX + ViewportWidth) / pixelsPerTick) + step;
            if (endTick > TotalTicks) endTick = TotalTicks;

            // 【优化 2】核心黑科技：把整个画板往左平移 VisibleOffsetX
            // 这样我们画出来的内容永远在屏幕 0 ~ ViewportWidth 之间，不需要几万像素的宽容度！
            dc.PushTransform(new TranslateTransform(-VisibleOffsetX, 0));

            // 开始疯狂画线！(这步在底层极快)
            for (int i = 0; i <= endTick; i += step)
            {
                double x = i * pixelsPerTick;

                // 1 小节 = 4拍 = 128 Ticks
                if (i % 128 == 0)
                {
                    // 画最高的小节线
                    dc.DrawLine(measurePen, new Point(x, 0), new Point(x, 24));

                    // 在小节线上方写上数字 (比如 0, 1, 2 小节)
                    FormattedText text = new FormattedText(
                        (i / 128).ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        11, // 字体大小
                        Brushes.White,
                        dpi);
                    dc.DrawText(text, new Point(x + 3, 2));
                }
                // 1 拍 = 32 Ticks
                else if (i % 32 == 0)
                {
                    // 画中等高度的拍线
                    dc.DrawLine(beatPen, new Point(x, 12), new Point(x, 24));
                }
                // 1/16 音符 = 8 Ticks
                else if (i % 8 == 0)
                {
                    // 画较短的细分线
                    dc.DrawLine(subPen, new Point(x, 18), new Point(x, 24));
                }
                // 1/32, 1/64, 1/128 等更小的单位
                else
                {
                    // 画最短的微小刻度
                    dc.DrawLine(subPen, new Point(x, 20), new Point(x, 24));
                }
            }

            // 画一条底部的横向基准基线
            

            // 画底部的基准横线（为了性能，也只画视野内的长度）
            dc.DrawLine(subPen, new Point(VisibleOffsetX, 24), new Point(VisibleOffsetX + ViewportWidth, 24));

            // 弹出平移变换
            dc.Pop();
        }
    }
}