using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Axphi.Components
{

    public class KeyFrame : Thumb
    {
        private Point _startMousePos;
        private int _startTick;

        public int Tick
        {
            get => (int)GetValue(TickProperty);
            set => SetValue(TickProperty, value);
        }

        public static readonly DependencyProperty TickProperty =
            DependencyProperty.Register("Tick", typeof(int), typeof(KeyFrame),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Zoom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(KeyFrame), new PropertyMetadata(1.0));



        public double PixelPerTick
        {
            get { return (double)GetValue(PixelPerTickProperty); }
            set { SetValue(PixelPerTickProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PixelPerTick.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PixelPerTickProperty =
            DependencyProperty.Register(nameof(PixelPerTick), typeof(double), typeof(KeyFrame), new PropertyMetadata(0.0));

        static KeyFrame()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KeyFrame), new FrameworkPropertyMetadata(typeof(KeyFrame)));
        }

        public KeyFrame()
        {
            // 在构造函数里挂载内置的拖拽事件
            DragStarted += OnDragStarted;
            DragDelta += OnDragDelta;
            // Cursor = Cursors.SizeWE;
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            _startMousePos = Mouse.GetPosition(Window.GetWindow(this));
            _startTick = Tick;

        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            
            double totalDeltaX = (Mouse.GetPosition(Window.GetWindow(this)) - _startMousePos).X;
            double pixelsPerTick = PixelPerTick * Zoom;
            int tickDelta = (int)Math.Round(totalDeltaX / pixelsPerTick, MidpointRounding.AwayFromZero);
            Tick = Math.Max(0, _startTick + tickDelta);
        }
    }
}