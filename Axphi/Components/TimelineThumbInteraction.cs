using Axphi.Utilities;
using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Axphi.Components
{
    public static class TimelineThumbInteraction
    {
        public static readonly DependencyProperty EnableDragProperty = DependencyProperty.RegisterAttached(
            "EnableDrag",
            typeof(bool),
            typeof(TimelineThumbInteraction),
            new PropertyMetadata(false, OnEnableDragChanged));

        public static readonly DependencyProperty EnableRightClickProperty = DependencyProperty.RegisterAttached(
            "EnableRightClick",
            typeof(bool),
            typeof(TimelineThumbInteraction),
            new PropertyMetadata(false, OnEnableRightClickChanged));

        private static readonly DependencyProperty DragTrackerProperty = DependencyProperty.RegisterAttached(
            "DragTracker",
            typeof(HorizontalDragTracker),
            typeof(TimelineThumbInteraction),
            new PropertyMetadata(null));

        public static void SetEnableDrag(DependencyObject element, bool value) => element.SetValue(EnableDragProperty, value);

        public static bool GetEnableDrag(DependencyObject element) => (bool)element.GetValue(EnableDragProperty);

        public static void SetEnableRightClick(DependencyObject element, bool value) => element.SetValue(EnableRightClickProperty, value);

        public static bool GetEnableRightClick(DependencyObject element) => (bool)element.GetValue(EnableRightClickProperty);

        private static HorizontalDragTracker GetOrCreateTracker(Thumb thumb)
        {
            if (thumb.GetValue(DragTrackerProperty) is HorizontalDragTracker tracker)
            {
                return tracker;
            }

            tracker = new HorizontalDragTracker();
            thumb.SetValue(DragTrackerProperty, tracker);
            return tracker;
        }

        private static UIElement GetDragReferenceElement(Thumb thumb)
        {
            return Window.GetWindow(thumb) is UIElement window
                ? window
                : thumb;
        }

        private static void OnEnableDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Thumb thumb)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                thumb.DragStarted += OnThumbDragStarted;
                thumb.DragDelta += OnThumbDragDelta;
                thumb.DragCompleted += OnThumbDragCompleted;
            }
            else
            {
                thumb.DragStarted -= OnThumbDragStarted;
                thumb.DragDelta -= OnThumbDragDelta;
                thumb.DragCompleted -= OnThumbDragCompleted;
            }
        }

        private static void OnEnableRightClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Thumb thumb)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                thumb.PreviewMouseRightButtonDown += OnThumbPreviewMouseRightButtonDown;
            }
            else
            {
                thumb.PreviewMouseRightButtonDown -= OnThumbPreviewMouseRightButtonDown;
            }
        }

        private static void OnThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is not Thumb thumb)
            {
                return;
            }

            HorizontalDragTracker tracker = GetOrCreateTracker(thumb);
            tracker.Start(GetDragReferenceElement(thumb));

            if (thumb.DataContext is ITimelineDraggable draggable)
            {
                draggable.OnDragStarted();
            }
        }

        private static void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb)
            {
                return;
            }

            if (thumb.DataContext is not ITimelineDraggable draggable)
            {
                return;
            }

            HorizontalDragTracker tracker = GetOrCreateTracker(thumb);
            double stableDelta = tracker.GetDeltaX(GetDragReferenceElement(thumb));
            draggable.OnDragDelta(stableDelta);
        }

        private static void OnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is Thumb { DataContext: ITimelineDraggable draggable })
            {
                draggable.OnDragCompleted();
            }
        }

        private static void OnThumbPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Thumb { DataContext: IRightClickableTimelineItem item })
            {
                item.OnRightClick();
                e.Handled = true;
            }
        }
    }
}
