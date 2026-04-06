using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Axphi.Components;

public static class HoldTailDragInteraction
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(HoldTailDragInteraction),
        new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DependencyObject element, bool value)
    {
        element.SetValue(EnableProperty, value);
    }

    public static bool GetEnable(DependencyObject element)
    {
        return (bool)element.GetValue(EnableProperty);
    }

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Thumb thumb)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            thumb.DragDelta += OnDragDelta;
        }
        else
        {
            thumb.DragDelta -= OnDragDelta;
        }
    }

    private static void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is not NoteViewModel noteViewModel)
        {
            return;
        }

        noteViewModel.ResizeHoldDurationByPixelDelta(e.HorizontalChange);
    }
}
