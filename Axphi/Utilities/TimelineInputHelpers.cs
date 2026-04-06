using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Axphi.Utilities
{
    public sealed class HorizontalDragTracker
    {
        private Point _lastMousePos;

        public void Start(UIElement relativeTo)
        {
            _lastMousePos = Mouse.GetPosition(relativeTo);
        }

        public double GetDeltaX(UIElement relativeTo)
        {
            Point currentPos = Mouse.GetPosition(relativeTo);
            double deltaX = currentPos.X - _lastMousePos.X;
            _lastMousePos = currentPos;
            return deltaX;
        }
    }

    public static class MouseWheelPassthrough
    {
        public static bool TryHandle(UIElement? sourceElement, MouseWheelEventArgs e)
        {
            if (sourceElement == null)
            {
                return false;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Shift)
            {
                return false;
            }

            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sourceElement,
            };

            if (VisualTreeHelper.GetParent(sourceElement) is UIElement parent)
            {
                parent.RaiseEvent(eventArg);
            }

            return true;
        }
    }
}
