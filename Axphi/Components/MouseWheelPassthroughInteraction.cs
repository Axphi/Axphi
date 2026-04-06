using Axphi.Utilities;
using System.Windows;
using System.Windows.Input;

namespace Axphi.Components;

public static class MouseWheelPassthroughInteraction
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(MouseWheelPassthroughInteraction),
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
        if (d is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        MouseWheelPassthrough.TryHandle(sender as UIElement, e);
    }
}
