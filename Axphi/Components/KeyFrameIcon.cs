using System;
using System.Collections.Generic;
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

namespace Axphi.Components
{
    public class KeyFrameIcon : FrameworkElement
    {
        static KeyFrameIcon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KeyFrameIcon), new FrameworkPropertyMetadata(typeof(KeyFrameIcon)));
        }

        private Geometry _iconGeometry;
        private Size _iconSize;

        public Brush Fill
        {
            get { return (Brush)GetValue(FillProperty); }
            set { SetValue(FillProperty, value); }
        }

        public Brush Stroke
        {
            get { return (Brush)GetValue(StrokeProperty); }
            set { SetValue(StrokeProperty, value); }
        }

        public double StrokeThickness
        {
            get { return (double)GetValue(StrokeThicknessProperty); }
            set { SetValue(StrokeThicknessProperty, value); }
        }


        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(KeyFrameIcon),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(KeyFrameIcon),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(KeyFrameIcon),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));



        public KeyFrameIcon()
        {
            _iconSize = new Size(8, 8);
            _iconGeometry = BuildIconGeometry(_iconSize);
        }

        private Geometry BuildIconGeometry(Size size)
        {
            double width = size.Width;
            double height = size.Height;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(width / 2, 0), true, true);
                context.LineTo(new Point(width, height / 2), true, false);
                context.LineTo(new Point(width / 2, height), true, false);
                context.LineTo(new Point(0, height / 2), true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                Math.Min(8, availableSize.Width),
                Math.Min(8, availableSize.Height));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_iconSize != RenderSize)
            {
                _iconSize = RenderSize;
                _iconGeometry = BuildIconGeometry(_iconSize);
            }

            drawingContext.DrawGeometry(Fill, new Pen(Stroke, StrokeThickness), _iconGeometry);
        }
    }
}
