using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Axphi.Components
{
    public class Playhead : Thumb
    {
        private Point _startMousePos;
        private int _startTick;

        static Playhead()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Playhead), new FrameworkPropertyMetadata(typeof(Playhead)));
        }

        public Playhead()
        {
            DragStarted += OnDragStarted;
            DragDelta += OnDragDelta;
            Cursor = Cursors.SizeWE; // 游标拖动专属光标
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            if (DataContext is TimeLineViewModel vm)
            {
                _startMousePos = Mouse.GetPosition(Window.GetWindow(this));
                _startTick = vm.CurrentTick;
            }
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is TimeLineViewModel vm)
            {
                Point currentMousePos = Mouse.GetPosition(Window.GetWindow(this));
                double totalDeltaX = currentMousePos.X - _startMousePos.X;

                vm.MovePlayheadByAbsoluteDelta(totalDeltaX, _startTick);
            }
        }
    }
}