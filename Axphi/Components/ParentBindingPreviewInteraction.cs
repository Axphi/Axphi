using Axphi.ViewModels;
using Axphi.Views;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Axphi.Components;

public static class ParentBindingPreviewInteraction
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(ParentBindingPreviewInteraction),
        new PropertyMetadata(false, OnEnableChanged));

    private static readonly DependencyProperty SourceTrackIdProperty = DependencyProperty.RegisterAttached(
        "SourceTrackId",
        typeof(string),
        typeof(ParentBindingPreviewInteraction),
        new PropertyMetadata(null));

    private static readonly DependencyProperty IsRegisteredProperty = DependencyProperty.RegisterAttached(
        "IsRegistered",
        typeof(bool),
        typeof(ParentBindingPreviewInteraction),
        new PropertyMetadata(false));

    public static void SetEnable(DependencyObject element, bool value)
    {
        element.SetValue(EnableProperty, value);
    }

    public static bool GetEnable(DependencyObject element)
    {
        return (bool)element.GetValue(EnableProperty);
    }

    private static string? GetSourceTrackId(DependencyObject element)
    {
        return (string?)element.GetValue(SourceTrackIdProperty);
    }

    private static void SetSourceTrackId(DependencyObject element, string? value)
    {
        element.SetValue(SourceTrackIdProperty, value);
    }

    private static bool GetIsRegistered(DependencyObject element)
    {
        return (bool)element.GetValue(IsRegisteredProperty);
    }

    private static void SetIsRegistered(DependencyObject element, bool value)
    {
        element.SetValue(IsRegisteredProperty, value);
    }

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Line line)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            line.Loaded += OnLoaded;
            line.Unloaded += OnUnloaded;

            if (line.IsLoaded)
            {
                Register(line);
            }
        }
        else
        {
            line.Loaded -= OnLoaded;
            line.Unloaded -= OnUnloaded;
            Unregister(line);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Line line)
        {
            Register(line);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Line line)
        {
            Unregister(line);
        }
    }

    private static void Register(Line line)
    {
        if (GetIsRegistered(line))
        {
            return;
        }

        WeakReferenceMessenger.Default.Register<Line, ParentBindingDragStartedMessage>(line, static (recipient, message) =>
        {
            SetSourceTrackId(recipient, message.SourceTrackId);
            recipient.X1 = message.StartPoint.X;
            recipient.Y1 = message.StartPoint.Y;
            recipient.X2 = message.StartPoint.X;
            recipient.Y2 = message.StartPoint.Y;
            recipient.Visibility = Visibility.Visible;
        });

        WeakReferenceMessenger.Default.Register<Line, ParentBindingDragUpdatedMessage>(line, static (recipient, message) =>
        {
            if (GetSourceTrackId(recipient) != message.SourceTrackId || recipient.Visibility != Visibility.Visible)
            {
                return;
            }

            recipient.X2 = message.CurrentPoint.X;
            recipient.Y2 = message.CurrentPoint.Y;
        });

        WeakReferenceMessenger.Default.Register<Line, ParentBindingDragCompletedMessage>(line, static (recipient, message) =>
        {
            if (GetSourceTrackId(recipient) != message.SourceTrackId)
            {
                HidePreviewLine(recipient);
                return;
            }

            var window = Window.GetWindow(recipient);
            if (window?.DataContext is MainViewModel vm)
            {
                var sourceTrack = vm.Timeline.Tracks.FirstOrDefault(track => track.Data.ID == message.SourceTrackId);
                var targetTrack = ResolveTrackFromWindowPoint(window, message.EndPoint);

                if (sourceTrack != null && targetTrack != null && !ReferenceEquals(sourceTrack, targetTrack))
                {
                    sourceTrack.ParentLineId = targetTrack.Data.ID;
                }
            }

            HidePreviewLine(recipient);
        });

        SetIsRegistered(line, true);
    }

    private static void Unregister(Line line)
    {
        if (!GetIsRegistered(line))
        {
            return;
        }

        WeakReferenceMessenger.Default.UnregisterAll(line);
        SetIsRegistered(line, false);
        HidePreviewLine(line);
    }

    private static TrackViewModel? ResolveTrackFromWindowPoint(Window window, Point windowPoint)
    {
        DependencyObject? hit = window.InputHitTest(windowPoint) as DependencyObject;
        while (hit != null)
        {
            if (hit is TrackControl trackControl && trackControl.DataContext is TrackViewModel trackViewModel)
            {
                return trackViewModel;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }

        return null;
    }

    private static void HidePreviewLine(Line line)
    {
        SetSourceTrackId(line, null);
        line.Visibility = Visibility.Collapsed;
    }
}
