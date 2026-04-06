using Axphi.ViewModels;
using Axphi.Views;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Axphi.Utilities;

public sealed class TimelineMarqueeSelectionService
{
    private FrameworkElement? _cachedMainBpmTrackControl;
    private FrameworkElement? _cachedMainAudioTrackControl;
    private ItemsControl? _cachedTrackItemsControl;

    public DependencyObject? ResolveSelectionScope(
        DependencyObject? current,
        Point mousePointInTimelineGrid,
        FrameworkElement timelineMainGrid,
        FrameworkElement root)
    {
        return GetMarqueeSelectionScope(current)
            ?? GetMarqueeSelectionScopeFromPoint(mousePointInTimelineGrid, timelineMainGrid, root);
    }

    public IEnumerable<Thumb> EnumerateCandidateThumbs(
        DependencyObject selectionRoot,
        Rect marqueeBounds,
        FrameworkElement timelineMainGrid,
        FrameworkElement root)
    {
        if (!ReferenceEquals(selectionRoot, timelineMainGrid))
        {
            return FindVisualChildren<Thumb>(selectionRoot);
        }

        var candidates = new List<Thumb>();
        foreach (var control in EnumerateMarqueeTrackControls(root))
        {
            if (!TryGetElementBoundsInTimeline(control, timelineMainGrid, out Rect controlBounds))
            {
                continue;
            }

            if (!controlBounds.IntersectsWith(marqueeBounds))
            {
                continue;
            }

            candidates.AddRange(FindVisualChildren<Thumb>(control));
        }

        return candidates;
    }

    public bool TryGetThumbBoundsInTimeline(Thumb thumb, FrameworkElement timelineMainGrid, out Rect thumbBounds)
    {
        thumbBounds = Rect.Empty;

        try
        {
            Rect localBounds = VisualTreeHelper.GetDescendantBounds(thumb);
            if (localBounds.IsEmpty || localBounds.Width <= 0 || localBounds.Height <= 0)
            {
                localBounds = new Rect(0, 0, thumb.ActualWidth, thumb.ActualHeight);
            }

            if (localBounds.IsEmpty || localBounds.Width <= 0 || localBounds.Height <= 0)
            {
                return false;
            }

            GeneralTransform transform = thumb.TransformToAncestor(timelineMainGrid);
            thumbBounds = transform.TransformBounds(localBounds);
            return thumbBounds.Width > 0 && thumbBounds.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsInNotePropertyKeyframeEditor(DependencyObject current)
    {
        while (current != null)
        {
            if (current is FrameworkElement frameworkElement && frameworkElement.Name == "NoteKeyframeEditorPanel")
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    public static bool IsNotePropertyKeyframeEditorExpanded(DependencyObject current)
    {
        while (current != null)
        {
            if (current is TrackControl trackControl && trackControl.DataContext is TrackViewModel trackViewModel)
            {
                return trackViewModel.IsExpanded && trackViewModel.IsNoteExpanded;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private DependencyObject? GetMarqueeSelectionScopeFromPoint(
        Point mousePointInTimelineGrid,
        FrameworkElement timelineMainGrid,
        FrameworkElement root)
    {
        foreach (var control in EnumerateMarqueeTrackControls(root))
        {
            if (IsPointInsideControl(control, mousePointInTimelineGrid, timelineMainGrid))
            {
                return control;
            }
        }

        return null;
    }

    private bool IsPointInsideControl(FrameworkElement control, Point mousePointInTimelineGrid, FrameworkElement timelineMainGrid)
    {
        if (!control.IsVisible || control.ActualWidth <= 0 || control.ActualHeight <= 0)
        {
            return false;
        }

        if (!TryGetElementBoundsInTimeline(control, timelineMainGrid, out Rect bounds))
        {
            return false;
        }

        return bounds.Contains(mousePointInTimelineGrid);
    }

    private static bool TryGetElementBoundsInTimeline(FrameworkElement control, FrameworkElement timelineMainGrid, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (!control.IsVisible || control.ActualWidth <= 0 || control.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            GeneralTransform transform = control.TransformToAncestor(timelineMainGrid);
            bounds = transform.TransformBounds(new Rect(0, 0, control.ActualWidth, control.ActualHeight));
            return bounds.Width > 0 && bounds.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static DependencyObject? GetMarqueeSelectionScope(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is TrackControl ||
                current is BpmTrackControl ||
                current is AudioTrackControl)
            {
                return current;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null)
        {
            yield break;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);

            if (child is T t)
            {
                yield return t;
            }

            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private IEnumerable<FrameworkElement> EnumerateMarqueeTrackControls(FrameworkElement root)
    {
        var bpmTrackControl = ResolveNamedFrameworkElement(root, "MainBpmTrackControl");
        if (bpmTrackControl?.IsVisible == true)
        {
            yield return bpmTrackControl;
        }

        var audioTrackControl = ResolveNamedFrameworkElement(root, "MainAudioTrackControl");
        if (audioTrackControl?.IsVisible == true)
        {
            yield return audioTrackControl;
        }

        var trackItemsControl = ResolveNamedItemsControl(root, "TrackItemsControl");
        if (trackItemsControl == null || trackItemsControl.Items.Count <= 0)
        {
            yield break;
        }

        for (int index = 0; index < trackItemsControl.Items.Count; index++)
        {
            var container = trackItemsControl.ItemContainerGenerator.ContainerFromIndex(index) as DependencyObject;
            if (container == null)
            {
                continue;
            }

            if (container is ContentPresenter presenter)
            {
                if (VisualTreeHelper.GetChildrenCount(presenter) > 0
                    && VisualTreeHelper.GetChild(presenter, 0) is TrackControl fastTrackControl
                    && fastTrackControl.IsVisible)
                {
                    yield return fastTrackControl;
                }

                continue;
            }

            var trackControl = FindVisualChild<TrackControl>(container);
            if (trackControl?.IsVisible == true)
            {
                yield return trackControl;
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T matched)
            {
                return matched;
            }

            var nested = FindVisualChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private FrameworkElement? ResolveNamedFrameworkElement(FrameworkElement root, string name)
    {
        if (name == "MainBpmTrackControl")
        {
            _cachedMainBpmTrackControl ??= root.FindName(name) as FrameworkElement
                ?? LogicalTreeHelper.FindLogicalNode(root, name) as FrameworkElement;
            return _cachedMainBpmTrackControl;
        }

        if (name == "MainAudioTrackControl")
        {
            _cachedMainAudioTrackControl ??= root.FindName(name) as FrameworkElement
                ?? LogicalTreeHelper.FindLogicalNode(root, name) as FrameworkElement;
            return _cachedMainAudioTrackControl;
        }

        return root.FindName(name) as FrameworkElement
            ?? LogicalTreeHelper.FindLogicalNode(root, name) as FrameworkElement;
    }

    private ItemsControl? ResolveNamedItemsControl(FrameworkElement root, string name)
    {
        if (_cachedTrackItemsControl != null)
        {
            return _cachedTrackItemsControl;
        }

        _cachedTrackItemsControl = root.FindName(name) as ItemsControl
            ?? LogicalTreeHelper.FindLogicalNode(root, name) as ItemsControl;

        return _cachedTrackItemsControl;
    }
}
