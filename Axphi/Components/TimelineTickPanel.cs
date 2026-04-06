using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Axphi.Components
{
    public class TimelineTickPanel : Panel
    {
        public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
            nameof(Timeline),
            typeof(TimelineViewModel),
            typeof(TimelineTickPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty LeftPaddingProperty = DependencyProperty.Register(
            nameof(LeftPadding),
            typeof(double),
            typeof(TimelineTickPanel),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty TickProperty = DependencyProperty.RegisterAttached(
            "Tick",
            typeof(double),
            typeof(TimelineTickPanel),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached(
            "Top",
            typeof(double),
            typeof(TimelineTickPanel),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public TimelineViewModel? Timeline
        {
            get => (TimelineViewModel?)GetValue(TimelineProperty);
            set => SetValue(TimelineProperty, value);
        }

        public double LeftPadding
        {
            get => (double)GetValue(LeftPaddingProperty);
            set => SetValue(LeftPaddingProperty, value);
        }

        public static void SetTick(DependencyObject element, double value)
        {
            element.SetValue(TickProperty, value);
        }

        public static double GetTick(DependencyObject element)
        {
            return (double)element.GetValue(TickProperty);
        }

        public static void SetTop(DependencyObject element, double value)
        {
            element.SetValue(TopProperty, value);
        }

        public static double GetTop(DependencyObject element)
        {
            return (double)element.GetValue(TopProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            }

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                double tick = GetTick(child);
                double x = LeftPadding + (Timeline?.TickToPixel(tick) ?? tick);
                double y = GetTop(child);
                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
            }

            return finalSize;
        }
    }
}
