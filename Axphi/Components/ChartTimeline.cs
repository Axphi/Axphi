using Axphi.Data;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Axphi.Components
{
    public class ChartTimeline : Control
    {
        static ChartTimeline()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChartTimeline), new FrameworkPropertyMetadata(typeof(ChartTimeline)));
        }

        public event EventHandler? ViewportChanged;

        public double EffectiveLengthPerSecond => Math.Max(0.01, LengthPerSecond * Scale);

        public double RulerHeight => 24;

        public TimeSpan Time
        {
            get { return (TimeSpan)GetValue(TimeProperty); }
            set { SetValue(TimeProperty, value); }
        }

        public TimeSpan PlayTime
        {
            get { return (TimeSpan)GetValue(PlayTimeProperty); }
            set { SetValue(PlayTimeProperty, value); }
        }

        public Chart Chart
        {
            get { return (Chart)GetValue(ChartProperty); }
            set { SetValue(ChartProperty, value); }
        }

        public double LengthPerSecond
        {
            get { return (double)GetValue(LengthPerSecondProperty); }
            set { SetValue(LengthPerSecondProperty, value); }
        }

        public double Scale
        {
            get { return (double)GetValue(ScaleProperty); }
            set { SetValue(ScaleProperty, value); }
        }

        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register(
                nameof(Time),
                typeof(TimeSpan),
                typeof(ChartTimeline),
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

        public static readonly DependencyProperty ChartProperty =
            DependencyProperty.Register(
                nameof(Chart),
                typeof(Chart),
                typeof(ChartTimeline),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LengthPerSecondProperty =
            DependencyProperty.Register(
                nameof(LengthPerSecond),
                typeof(double),
                typeof(ChartTimeline),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register(
                nameof(Scale),
                typeof(double),
                typeof(ChartTimeline),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnViewportPropertyChanged));

        public static readonly DependencyProperty PlayTimeProperty =
            DependencyProperty.Register(nameof(PlayTime), typeof(TimeSpan), typeof(ChartTimeline), 
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double GetTimelineX(TimeSpan absoluteTime)
        {
            return (absoluteTime - Time).TotalSeconds * EffectiveLengthPerSecond;
        }

        public TimeSpan GetTimeAtTimelineX(double x)
        {
            return Time + TimeSpan.FromSeconds(x / EffectiveLengthPerSecond);
        }

        private static void OnViewportPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var timeline = (ChartTimeline)d;
            timeline.ViewportChanged?.Invoke(timeline, EventArgs.Empty);
        }
    }
}
