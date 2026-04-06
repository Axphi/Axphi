using Axphi.Utilities;
using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Axphi.Components
{
    public enum LayerResizeEdge
    {
        Left,
        Right,
    }

    public static class LayerResizeThumbInteraction
    {
        public static readonly DependencyProperty EdgeProperty = DependencyProperty.RegisterAttached(
            "Edge",
            typeof(LayerResizeEdge),
            typeof(LayerResizeThumbInteraction),
            new PropertyMetadata(LayerResizeEdge.Left, OnEdgeChanged));

        private static readonly DependencyProperty DragTrackerProperty = DependencyProperty.RegisterAttached(
            "DragTracker",
            typeof(HorizontalDragTracker),
            typeof(LayerResizeThumbInteraction),
            new PropertyMetadata(null));

        public static void SetEdge(DependencyObject element, LayerResizeEdge value) => element.SetValue(EdgeProperty, value);

        public static LayerResizeEdge GetEdge(DependencyObject element) => (LayerResizeEdge)element.GetValue(EdgeProperty);

        private static void OnEdgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Thumb thumb)
            {
                return;
            }

            thumb.DragStarted -= OnThumbDragStarted;
            thumb.DragDelta -= OnThumbDragDelta;
            thumb.DragCompleted -= OnThumbDragCompleted;

            thumb.DragStarted += OnThumbDragStarted;
            thumb.DragDelta += OnThumbDragDelta;
            thumb.DragCompleted += OnThumbDragCompleted;
        }

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

        private static void OnThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is not Thumb thumb)
            {
                return;
            }

            HorizontalDragTracker tracker = GetOrCreateTracker(thumb);
            tracker.Start(GetDragReferenceElement(thumb));

            if (thumb.DataContext is ILayerResizable resizable)
            {
                if (GetEdge(thumb) == LayerResizeEdge.Left)
                {
                    resizable.BeginResizeLeft();
                }
                else
                {
                    resizable.BeginResizeRight();
                }
            }

            e.Handled = true;
        }

        private static void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb)
            {
                return;
            }

            if (thumb.DataContext is not ILayerResizable resizable)
            {
                return;
            }

            HorizontalDragTracker tracker = GetOrCreateTracker(thumb);
            double stableDelta = tracker.GetDeltaX(GetDragReferenceElement(thumb));

            if (GetEdge(thumb) == LayerResizeEdge.Left)
            {
                resizable.ResizeLeft(stableDelta);
            }
            else
            {
                resizable.ResizeRight(stableDelta);
            }

            e.Handled = true;
        }

        private static void OnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is Thumb { DataContext: ILayerResizable resizable } thumb)
            {
                if (GetEdge(thumb) == LayerResizeEdge.Left)
                {
                    resizable.EndResizeLeft();
                }
                else
                {
                    resizable.EndResizeRight();
                }
            }

            e.Handled = true;
        }
    }
}
