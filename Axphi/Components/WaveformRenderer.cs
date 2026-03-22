using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Axphi.Components
{
    public class WaveformRenderer : FrameworkElement
    {
        // ================= 🌟 核心修复 =================
        // 把 typeof(float[]) 改成 typeof(IEnumerable<float>) 
        // 完美欺骗 WPF 编译器，绕过模板里的原生数组 Bug！
        public static readonly DependencyProperty PeaksProperty =
            DependencyProperty.Register("Peaks", typeof(IEnumerable<float>), typeof(WaveformRenderer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<float>? Peaks
        {
            get { return (IEnumerable<float>?)GetValue(PeaksProperty); }
            set { SetValue(PeaksProperty, value); }
        }

        public static readonly DependencyProperty WaveColorProperty =
            DependencyProperty.Register("WaveColor", typeof(Brush), typeof(WaveformRenderer),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(180, 40, 232, 87)), FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush WaveColor
        {
            get { return (Brush)GetValue(WaveColorProperty); }
            set { SetValue(WaveColorProperty, value); }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (Peaks == null) return;

            // 🌟 极限性能优化：因为我们知道 ViewModel 传过来的一定是 float[]
            // 我们在 C# 代码里把它强转回数组，这样取数据的速度是最快的！
            float[]? peaksArray = Peaks as float[];
            if (peaksArray == null || peaksArray.Length == 0) return;

            double width = ActualWidth;
            double height = ActualHeight;
            double midY = height / 2;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                double stepX = width / peaksArray.Length;

                // 🌟 算出一个缩放倍数 (100 = 1.0倍, 50 = 0.5倍)
                double volumeScale = Math.Max(0, Volume / 100.0);

                ctx.BeginFigure(new Point(0, midY), true, true);

                for (int i = 0; i < peaksArray.Length; i++)
                {
                    double x = i * stepX;
                    // 🌟 高度乘以 volumeScale！
                    double y = midY - (peaksArray[i] * midY * volumeScale);
                    ctx.LineTo(new Point(x, y), true, false);
                }

                for (int i = peaksArray.Length - 1; i >= 0; i--)
                {
                    double x = i * stepX;
                    // 🌟 高度乘以 volumeScale！
                    double y = midY + (peaksArray[i] * midY * volumeScale);
                    ctx.LineTo(new Point(x, y), true, false);
                }
            }
            geometry.Freeze();

            drawingContext.DrawGeometry(WaveColor, null, geometry);
        }


        // ================= 🌟 新增：接收前端传来的音量大小 =================
        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register("Volume", typeof(double), typeof(WaveformRenderer),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender)); // AffectsRender 意味着拖拽时会自动重绘波形！

        public double Volume
        {
            get { return (double)GetValue(VolumeProperty); }
            set { SetValue(VolumeProperty, value); }
        }
    }
}