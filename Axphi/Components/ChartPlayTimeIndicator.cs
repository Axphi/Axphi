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
using NAudio.MediaFoundation;

namespace Axphi.Components
{
    /// <summary>
    /// 负责显示当前播放时间的红色光标, 并允许拖拽调整时间
    /// </summary>
    public class ChartPlayTimeIndicator : TimelineTrack
    {
        static ChartPlayTimeIndicator()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChartPlayTimeIndicator), new FrameworkPropertyMetadata(typeof(ChartPlayTimeIndicator)));
        }

        private readonly Brush _brush;
        private readonly Pen _indicatorPen;
        private readonly Geometry _triangleGeometry = new PathGeometry()
        {
            Figures = new PathFigureCollection()
            {
                new PathFigure()
                {
                    StartPoint = new Point(-5, 0),
                    Segments = new PathSegmentCollection()
                    {
                        new LineSegment(new Point(5, 0), true),
                        new LineSegment(new Point(0, 7), true)
                    },
                    IsClosed = true
                }
            }
        };

        private TranslateTransform _translateTransform = new TranslateTransform();

        private double _dragStartTimeX;
        private double _dragStartMouseX;

        public TimeSpan PlayTime
        {
            get { return (TimeSpan)GetValue(PlayTimeProperty); }
            set { SetValue(PlayTimeProperty, value); }
        }

        public bool AllowDragAdjustPlayTime
        {
            get { return (bool)GetValue(AllowDragAdjustPlayTimeProperty); }
            set { SetValue(AllowDragAdjustPlayTimeProperty, value); }
        }

        public ChartPlayTimeIndicator()
        {
            _brush = Brushes.Red;
            _indicatorPen = new Pen(_brush, 1);
        }

        public static readonly DependencyProperty PlayTimeProperty =
            DependencyProperty.Register(nameof(PlayTime), typeof(TimeSpan), typeof(ChartPlayTimeIndicator),
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty AllowDragAdjustPlayTimeProperty =
            DependencyProperty.Register(nameof(AllowDragAdjustPlayTime), typeof(bool), typeof(ChartPlayTimeIndicator), 
                new FrameworkPropertyMetadata(false));

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Context is { } context)
            {
                _translateTransform.X = context.GetTimelineX(PlayTime);

                dc.PushTransform(_translateTransform);
                dc.DrawGeometry(_brush, null, _triangleGeometry);
                dc.DrawLine(_indicatorPen, new Point(0, 0), new Point(0, ActualHeight));
                dc.Pop();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (AllowDragAdjustPlayTime &&
                Context is { } context &&
                CaptureMouse())
            {
                _dragStartTimeX = context.GetTimelineX(PlayTime);
                _dragStartMouseX = e.GetPosition(this).X;
                e.Handled = true;
                return;
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (IsMouseCaptured &&
                Context is { } context)
            {
                var currentMouseX = e.GetPosition(this).X;
                var newTime = context.GetTimeAtTimelineX(_dragStartTimeX + (currentMouseX - _dragStartMouseX));

                PlayTime = newTime < TimeSpan.Zero ?
                    TimeSpan.Zero :
                    newTime;

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
    }
}
