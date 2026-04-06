using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Axphi.Components;

public static class GlobalHorizontalScrollInteraction
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(GlobalHorizontalScrollInteraction),
        new PropertyMetadata(false, OnEnableChanged));

    public static readonly DependencyProperty TimelineProperty = DependencyProperty.RegisterAttached(
        "Timeline",
        typeof(TimelineViewModel),
        typeof(GlobalHorizontalScrollInteraction),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PlayheadTransformProperty = DependencyProperty.RegisterAttached(
        "PlayheadTransform",
        typeof(TranslateTransform),
        typeof(GlobalHorizontalScrollInteraction),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ViewportReferenceProperty = DependencyProperty.RegisterAttached(
        "ViewportReference",
        typeof(FrameworkElement),
        typeof(GlobalHorizontalScrollInteraction),
        new PropertyMetadata(null));

    public static readonly DependencyProperty BasePixelsPerTickProperty = DependencyProperty.RegisterAttached(
        "BasePixelsPerTick",
        typeof(double),
        typeof(GlobalHorizontalScrollInteraction),
        new PropertyMetadata(0.5));

    public static readonly DependencyProperty RightPaddingProperty = DependencyProperty.RegisterAttached(
        "RightPadding",
        typeof(double),
        typeof(GlobalHorizontalScrollInteraction),
        new PropertyMetadata(15.0));

    public static void SetEnable(DependencyObject element, bool value) => element.SetValue(EnableProperty, value);
    public static bool GetEnable(DependencyObject element) => (bool)element.GetValue(EnableProperty);

    public static void SetTimeline(DependencyObject element, TimelineViewModel? value) => element.SetValue(TimelineProperty, value);
    public static TimelineViewModel? GetTimeline(DependencyObject element) => (TimelineViewModel?)element.GetValue(TimelineProperty);

    public static void SetPlayheadTransform(DependencyObject element, TranslateTransform? value) => element.SetValue(PlayheadTransformProperty, value);
    public static TranslateTransform? GetPlayheadTransform(DependencyObject element) => (TranslateTransform?)element.GetValue(PlayheadTransformProperty);

    public static void SetViewportReference(DependencyObject element, FrameworkElement? value) => element.SetValue(ViewportReferenceProperty, value);
    public static FrameworkElement? GetViewportReference(DependencyObject element) => (FrameworkElement?)element.GetValue(ViewportReferenceProperty);

    public static void SetBasePixelsPerTick(DependencyObject element, double value) => element.SetValue(BasePixelsPerTickProperty, value);
    public static double GetBasePixelsPerTick(DependencyObject element) => (double)element.GetValue(BasePixelsPerTickProperty);

    public static void SetRightPadding(DependencyObject element, double value) => element.SetValue(RightPaddingProperty, value);
    public static double GetRightPadding(DependencyObject element) => (double)element.GetValue(RightPaddingProperty);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollBar scrollBar)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollBar.ValueChanged += OnScrollBarValueChanged;
        }
        else
        {
            scrollBar.ValueChanged -= OnScrollBarValueChanged;
        }
    }

    private static void OnScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not ScrollBar scrollBar)
        {
            return;
        }

        double offset = e.NewValue;

        var timeline = GetTimeline(scrollBar);
        if (timeline != null)
        {
            timeline.CurrentHorizontalScrollOffset = offset;
        }

        WeakReferenceMessenger.Default.Send(new SyncHorizontalScrollMessage(offset));

        var playheadTransform = GetPlayheadTransform(scrollBar);
        if (playheadTransform != null)
        {
            playheadTransform.X = -offset;
        }

        UpdateViewportState(scrollBar, timeline, offset);
    }

    private static void UpdateViewportState(ScrollBar scrollBar, TimelineViewModel? timeline, double leftPixel)
    {
        if (timeline == null)
        {
            return;
        }

        var viewportReference = GetViewportReference(scrollBar);
        if (viewportReference == null)
        {
            return;
        }

        double visiblePixels = viewportReference.ActualWidth;
        if (visiblePixels <= 0)
        {
            return;
        }

        timeline.ViewportActualWidth = visiblePixels;

        double basePixelsPerTick = Math.Max(0.000001, GetBasePixelsPerTick(scrollBar));
        double rightPadding = GetRightPadding(scrollBar);

        double denominator = Math.Max(1, timeline.TotalDurationTicks) * basePixelsPerTick;
        double minScale = Math.Max(0.01, (visiblePixels - rightPadding) / denominator);

        if (timeline.ZoomScale < minScale)
        {
            timeline.ZoomScale = minScale;
            if (scrollBar.Value > 0)
            {
                scrollBar.SetCurrentValue(RangeBase.ValueProperty, 0d);
            }

            leftPixel = 0;
        }

        timeline.ViewportStartTick = timeline.PixelToTick(leftPixel);
        timeline.ViewportEndTick = timeline.PixelToTick(leftPixel + visiblePixels);
    }
}
