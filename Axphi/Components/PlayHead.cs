using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Axphi.Components
{
    public class Playhead : Thumb
    {



        public double CurrentTick
        {
            get { return (double)GetValue(CurrentTickProperty); }
            set { SetValue(CurrentTickProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentTick.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentTickProperty =
            DependencyProperty.Register(nameof(CurrentTick), typeof(double), typeof(Playhead), new PropertyMetadata(0.0));




        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Zoom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(Playhead), new PropertyMetadata(1.0));



        public double PixelPerTick
        {
            get { return (double)GetValue(PixelPerTickProperty); }
            set { SetValue(PixelPerTickProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PixelPerTick.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PixelPerTickProperty =
            DependencyProperty.Register(nameof(PixelPerTick), typeof(double), typeof(Playhead), new PropertyMetadata(0.0));




        




        private Point _startMousePos;
        private double _startTick;

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
            // 以 window 参考
            _startMousePos = Mouse.GetPosition(Window.GetWindow(this));
            _startTick = CurrentTick;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            Point currentMousePos = Mouse.GetPosition(Window.GetWindow(this));
            double totalDeltaX = currentMousePos.X - _startMousePos.X;

            double pixelsPerTick = PixelPerTick * Zoom;
            

            double tickDelta = Math.Round(totalDeltaX / pixelsPerTick, MidpointRounding.AwayFromZero);
            
            double newTick = Math.Max(0.0, _startTick + tickDelta);
            CurrentTick = newTick;
        }
    }
}