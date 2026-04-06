using Axphi.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Axphi.Components
{
    public static class LayerPointerInteraction
    {
        public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(LayerPointerInteraction),
            new PropertyMetadata(false, OnEnableChanged));

        public static readonly DependencyProperty IgnoreDoubleClickProperty = DependencyProperty.RegisterAttached(
            "IgnoreDoubleClick",
            typeof(bool),
            typeof(LayerPointerInteraction),
            new PropertyMetadata(false));

        public static readonly DependencyProperty IgnorePointerSelectionProperty = DependencyProperty.RegisterAttached(
            "IgnorePointerSelection",
            typeof(bool),
            typeof(LayerPointerInteraction),
            new PropertyMetadata(false));

        public static void SetEnable(DependencyObject element, bool value) => element.SetValue(EnableProperty, value);

        public static bool GetEnable(DependencyObject element) => (bool)element.GetValue(EnableProperty);

        public static void SetIgnoreDoubleClick(DependencyObject element, bool value) => element.SetValue(IgnoreDoubleClickProperty, value);

        public static bool GetIgnoreDoubleClick(DependencyObject element) => (bool)element.GetValue(IgnoreDoubleClickProperty);

        public static void SetIgnorePointerSelection(DependencyObject element, bool value) => element.SetValue(IgnorePointerSelectionProperty, value);

        public static bool GetIgnorePointerSelection(DependencyObject element) => (bool)element.GetValue(IgnorePointerSelectionProperty);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                element.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            }
            else
            {
                element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                element.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element && GetIgnoreDoubleClick(element) && e.ClickCount == 2)
            {
                return;
            }

            if (sender is FrameworkElement scopeElement && IsFromIgnoredElement(e.OriginalSource as DependencyObject, scopeElement))
            {
                return;
            }

            if (sender is FrameworkElement { DataContext: ILayerPointerInteractable interactable })
            {
                interactable.HandleLayerPointerDown();
            }
        }

        private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element && GetIgnoreDoubleClick(element) && e.ClickCount == 2)
            {
                return;
            }

            if (sender is FrameworkElement scopeElement && IsFromIgnoredElement(e.OriginalSource as DependencyObject, scopeElement))
            {
                return;
            }

            if (sender is FrameworkElement { DataContext: ILayerPointerInteractable interactable })
            {
                interactable.HandleLayerPointerUp();
            }
        }

        private static bool IsFromIgnoredElement(DependencyObject? source, FrameworkElement scopeElement)
        {
            DependencyObject? current = source;
            while (current != null && !ReferenceEquals(current, scopeElement))
            {
                if (GetIgnorePointerSelection(current))
                {
                    return true;
                }

                current = current is Visual or Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                    : null;
            }

            return false;
        }
    }
}
