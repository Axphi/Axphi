using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Axphi.Components
{
    // 🌟 直接继承 Thumb，天生自带 Drag 事件！
    public class KeyFrame : Thumb
    {
        private Point _startMousePos;
        private int _startTick;

        static KeyFrame()
        {
            // 告诉 WPF：去 Themes/Generic.xaml 里找我的默认样式
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
            if (DataContext is TimeLineKeyFrameViewModel vm)
            {
                _startMousePos = Mouse.GetPosition(Window.GetWindow(this));
                _startTick = vm.KeyFrameData.Tick;
            }
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is TimeLineKeyFrameViewModel vm)
            {
                Point currentMousePos = Mouse.GetPosition(Window.GetWindow(this));
                double totalDeltaX = currentMousePos.X - _startMousePos.X;

                vm.MoveTickByAbsoluteDelta(totalDeltaX, _startTick);
            }
        }
    }
}