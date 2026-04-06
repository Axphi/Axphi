using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace Axphi.Views
{
    public partial class BpmTrackControl : UserControl
    {
        private readonly HorizontalDragTracker _dragTracker = new();

        public BpmTrackControl()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            WeakReferenceMessenger.Default.Register<BpmTrackControl, SyncHorizontalScrollMessage>(this, static (recipient, message) =>
            {
                recipient.BpmTrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyCurrentHorizontalOffset();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void ApplyCurrentHorizontalOffset()
        {
            if (DataContext is BpmTrackViewModel bpmTrackViewModel)
            {
                BpmTrackScrollViewer.ScrollToHorizontalOffset(bpmTrackViewModel.Timeline.CurrentHorizontalScrollOffset);
            }
        }

        private void InnerTrack_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MouseWheelPassthrough.TryHandle(sender as UIElement, e);
        }

        private static bool TryGetTimelineDraggable(object sender, out ITimelineDraggable draggable)
        {
            if (sender is FrameworkElement { DataContext: ITimelineDraggable timelineDraggable })
            {
                draggable = timelineDraggable;
                return true;
            }

            draggable = default!;
            return false;
        }

        private void KeyframeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            _dragTracker.Start(this);

            if (TryGetTimelineDraggable(sender, out var draggable))
            {
                draggable.OnDragStarted();
            }
        }

        private void KeyframeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double stableDelta = _dragTracker.GetDeltaX(this);

            if (TryGetTimelineDraggable(sender, out var draggable))
            {
                draggable.OnDragDelta(stableDelta);
            }
        }

        private void KeyframeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (TryGetTimelineDraggable(sender, out var draggable))
            {
                draggable.OnDragCompleted();
            }
        }
    }
}
