using System;
using System.Windows;
using System.Windows.Controls;

namespace Axphi.Components
{
    public class TimelineTrackPanel : Panel
    {
        public ChartTimeline? Context
        {
            get { return (ChartTimeline?)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        public static ChartTimeline? GetContext(DependencyObject obj)
        {
            return (ChartTimeline?)obj.GetValue(ContextProperty);
        }

        public static void SetContext(DependencyObject obj, ChartTimeline? value)
        {
            obj.SetValue(ContextProperty, value);
        }

        public static TimeSpan GetTime(DependencyObject obj)
        {
            return (TimeSpan)obj.GetValue(TimeProperty);
        }

        public static void SetTime(DependencyObject obj, TimeSpan value)
        {
            obj.SetValue(TimeProperty, value);
        }

        public static readonly DependencyProperty ContextProperty =
            DependencyProperty.Register(
                nameof(Context),
                typeof(ChartTimeline),
                typeof(TimelineTrackPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange, OnContextChanged));

        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.RegisterAttached(
                "Time",
                typeof(TimeSpan),
                typeof(TimelineTrackPanel),
                new FrameworkPropertyMetadata(default(TimeSpan), FrameworkPropertyMetadataOptions.AffectsParentArrange));

        protected override Size MeasureOverride(Size availableSize)
        {
            var maxHeight = 0.0;
            var maxWidth = 0.0;

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(availableSize);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
                maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
            }

            var desiredWidth = double.IsInfinity(availableSize.Width) ? maxWidth : availableSize.Width;
            var desiredHeight = double.IsInfinity(availableSize.Height) ? maxHeight : Math.Min(availableSize.Height, maxHeight);

            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                var childTime = GetTime(child);
                var x = Context?.GetTimelineX(childTime) ?? 0;
                child.Arrange(new Rect(new Point(x, 0), child.DesiredSize));
            }

            return finalSize;
        }

        private static void OnContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (TimelineTrackPanel)d;

            if (e.OldValue is ChartTimeline oldContext)
            {
                oldContext.ViewportChanged -= panel.Context_ViewportChanged;
            }

            if (e.NewValue is ChartTimeline newContext)
            {
                newContext.ViewportChanged += panel.Context_ViewportChanged;
            }

            panel.InvalidateArrange();
        }

        private void Context_ViewportChanged(object? sender, EventArgs e)
        {
            InvalidateArrange();
        }
    }
}