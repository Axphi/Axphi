using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;

namespace Axphi.Components;

public static class HorizontalScrollSyncInteraction
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(HorizontalScrollSyncInteraction),
        new PropertyMetadata(false, OnEnableChanged));

    public static readonly DependencyProperty TimelineProperty = DependencyProperty.RegisterAttached(
        "Timeline",
        typeof(TimelineViewModel),
        typeof(HorizontalScrollSyncInteraction),
        new PropertyMetadata(null, OnTimelineChanged));

    private static readonly DependencyProperty IsRegisteredProperty = DependencyProperty.RegisterAttached(
        "IsRegistered",
        typeof(bool),
        typeof(HorizontalScrollSyncInteraction),
        new PropertyMetadata(false));

    public static void SetEnable(DependencyObject element, bool value)
    {
        element.SetValue(EnableProperty, value);
    }

    public static bool GetEnable(DependencyObject element)
    {
        return (bool)element.GetValue(EnableProperty);
    }

    public static void SetTimeline(DependencyObject element, TimelineViewModel? value)
    {
        element.SetValue(TimelineProperty, value);
    }

    public static TimelineViewModel? GetTimeline(DependencyObject element)
    {
        return (TimelineViewModel?)element.GetValue(TimelineProperty);
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
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollViewer.Loaded += OnLoaded;
            scrollViewer.Unloaded += OnUnloaded;

            if (scrollViewer.IsLoaded)
            {
                Register(scrollViewer);
            }
        }
        else
        {
            scrollViewer.Loaded -= OnLoaded;
            scrollViewer.Unloaded -= OnUnloaded;
            Unregister(scrollViewer);
        }
    }

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer && scrollViewer.IsLoaded)
        {
            ApplyCurrentHorizontalOffset(scrollViewer);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            Register(scrollViewer);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            Unregister(scrollViewer);
        }
    }

    private static void Register(ScrollViewer scrollViewer)
    {
        if (GetIsRegistered(scrollViewer))
        {
            ApplyCurrentHorizontalOffset(scrollViewer);
            return;
        }

        WeakReferenceMessenger.Default.Register<ScrollViewer, SyncHorizontalScrollMessage>(scrollViewer, (recipient, message) =>
        {
            recipient.ScrollToHorizontalOffset(message.Offset);
        });

        SetIsRegistered(scrollViewer, true);
        ApplyCurrentHorizontalOffset(scrollViewer);
    }

    private static void Unregister(ScrollViewer scrollViewer)
    {
        if (!GetIsRegistered(scrollViewer))
        {
            return;
        }

        WeakReferenceMessenger.Default.UnregisterAll(scrollViewer);
        SetIsRegistered(scrollViewer, false);
    }

    private static void ApplyCurrentHorizontalOffset(ScrollViewer scrollViewer)
    {
        var timeline = GetTimeline(scrollViewer);
        if (timeline == null)
        {
            return;
        }

        scrollViewer.ScrollToHorizontalOffset(timeline.CurrentHorizontalScrollOffset);
    }
}
