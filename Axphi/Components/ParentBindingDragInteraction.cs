using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Input;

namespace Axphi.Components;

public static class ParentBindingDragInteraction
{
    private sealed class DragState
    {
        public required string SourceTrackId { get; init; }
        public required Window Window { get; init; }
        public required MouseEventHandler MouseMoveHandler { get; init; }
        public required MouseButtonEventHandler MouseUpHandler { get; init; }
    }

    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(ParentBindingDragInteraction),
        new PropertyMetadata(false, OnEnableChanged));

    private static readonly DependencyProperty DragStateProperty = DependencyProperty.RegisterAttached(
        "DragState",
        typeof(DragState),
        typeof(ParentBindingDragInteraction),
        new PropertyMetadata(null));

    public static void SetEnable(DependencyObject element, bool value)
    {
        element.SetValue(EnableProperty, value);
    }

    public static bool GetEnable(DependencyObject element)
    {
        return (bool)element.GetValue(EnableProperty);
    }

    private static DragState? GetDragState(DependencyObject element)
    {
        return (DragState?)element.GetValue(DragStateProperty);
    }

    private static void SetDragState(DependencyObject element, DragState? value)
    {
        element.SetValue(DragStateProperty, value);
    }

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            element.Unloaded += OnUnloaded;
        }
        else
        {
            element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            element.Unloaded -= OnUnloaded;
            EndDrag(element);
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is not TrackViewModel sourceTrack)
        {
            return;
        }

        var window = Window.GetWindow(element);
        if (window == null)
        {
            return;
        }

        Point startPoint = element.TranslatePoint(
            new Point(element.ActualWidth / 2.0, element.ActualHeight / 2.0),
            window);

        WeakReferenceMessenger.Default.Send(new ParentBindingDragStartedMessage(sourceTrack.Data.ID, startPoint));

        MouseEventHandler moveHandler = (_, args) =>
        {
            var state = GetDragState(element);
            if (state == null)
            {
                return;
            }

            Point currentPoint = args.GetPosition(state.Window);
            WeakReferenceMessenger.Default.Send(new ParentBindingDragUpdatedMessage(state.SourceTrackId, currentPoint));
        };

        MouseButtonEventHandler upHandler = (_, args) =>
        {
            var state = GetDragState(element);
            if (state == null)
            {
                return;
            }

            Point endPoint = args.GetPosition(state.Window);
            WeakReferenceMessenger.Default.Send(new ParentBindingDragCompletedMessage(state.SourceTrackId, endPoint));
            EndDrag(element);
        };

        SetDragState(element, new DragState
        {
            SourceTrackId = sourceTrack.Data.ID,
            Window = window,
            MouseMoveHandler = moveHandler,
            MouseUpHandler = upHandler
        });

        window.PreviewMouseMove += moveHandler;
        window.PreviewMouseLeftButtonUp += upHandler;

        Mouse.Capture(element, CaptureMode.SubTree);
        e.Handled = true;
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            EndDrag(element);
        }
    }

    private static void EndDrag(FrameworkElement element)
    {
        var state = GetDragState(element);
        if (state != null)
        {
            state.Window.PreviewMouseMove -= state.MouseMoveHandler;
            state.Window.PreviewMouseLeftButtonUp -= state.MouseUpHandler;
            SetDragState(element, null);
        }

        if (ReferenceEquals(Mouse.Captured, element))
        {
            Mouse.Capture(null);
        }
    }
}
