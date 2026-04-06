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
    /// 按照步骤 1a 或 1b 操作，然后执行步骤 2 以在 XAML 文件中使用此自定义控件。
    ///
    /// 步骤 1a) 在当前项目中存在的 XAML 文件中使用该自定义控件。
    /// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
    /// 元素中:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Axphi.Components"
    ///
    ///
    /// 步骤 1b) 在其他项目中存在的 XAML 文件中使用该自定义控件。
    /// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
    /// 元素中:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Axphi.Components;assembly=Axphi.Components"
    ///
    /// 您还需要添加一个从 XAML 文件所在的项目到此项目的项目引用，
    /// 并重新生成以避免编译错误:
    ///
    ///     在解决方案资源管理器中右击目标项目，然后依次单击
    ///     “添加引用”->“项目”->[浏览查找并选择此项目]
    ///
    ///
    /// 步骤 2)
    /// 继续操作并在 XAML 文件中使用控件。
    ///
    ///     <MyNamespace:ChartPlayTimeIndicator/>
    ///
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

        public bool AllowDragMove
        {
            get { return (bool)GetValue(AllowDragMoveProperty); }
            set { SetValue(AllowDragMoveProperty, value); }
        }

        public ChartPlayTimeIndicator()
        {
            _brush = Brushes.Red;
            _indicatorPen = new Pen(_brush, 1);
        }

        // Using a DependencyProperty as the backing store for PlayTime.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlayTimeProperty =
            DependencyProperty.Register(nameof(PlayTime), typeof(TimeSpan), typeof(ChartPlayTimeIndicator),
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        // Using a DependencyProperty as the backing store for AllowDragMove.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AllowDragMoveProperty =
            DependencyProperty.Register(nameof(AllowDragMove), typeof(bool), typeof(ChartPlayTimeIndicator), new PropertyMetadata(false));

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
            if (AllowDragMove &&
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
