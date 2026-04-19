using Nodify;
using System;
using System.Collections;
using System.Windows;
using System.Windows.Input;

namespace Axphi.Components
{
    public class TimeLine : NodifyEditor
    {

        static double minX = -5;
        static TimeLine()
        {
            // 覆盖 ViewportLocation 的元数据，添加强制转换回调
            ViewportLocationProperty.OverrideMetadata(
                typeof(TimeLine),
                new FrameworkPropertyMetadata(
                    new Point(minX, 0), // 默认值
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    null, // PropertyChangedCallback
                    CoerceViewportLocation // 在这里拦截并修改坐标
                )
            );
        }

        private static object CoerceViewportLocation(DependencyObject d, object baseValue)
        {
            if (baseValue is Point point)
            {
                // 🌟 2. 相机限制：强制 X 和 Y 永远大于等于 0
                return new Point(Math.Max(minX, point.X), Math.Max(0, point.Y));
            }
            return new Point(minX, 0);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            // 拦截 Nodify 自带的滚轮行为
            e.Handled = true;

            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                // 🌟 3. Alt + 滚轮：缩放 (修改 Zoom 依赖属性)
                double zoomSpeed = 0.05; // 缩放灵敏度
                double newZoom = Zoom + (e.Delta > 0 ? zoomSpeed : -zoomSpeed);
                // 钳制缩放比例，防止缩得太小或放得太大导致渲染崩溃
                Zoom = Math.Clamp(newZoom, 0.1, 5.0);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // 🌟 4. Shift + 滚轮：横向滚动 (X轴)
                double scrollSpeed = 0.1; // X轴滚动灵敏度, 丢到设置
                double deltaX = e.Delta * scrollSpeed;
                double newX = ViewportLocation.X - deltaX;

                ViewportLocation = new Point(Math.Max(minX, newX), ViewportLocation.Y);
            }
            else
            {
                // 🌟 5. 默认滚轮：纵向滚动 (Y轴)，速度和 TrackHeader 保持一致
                double scrollSpeed = 0.1;
                double deltaY = e.Delta * scrollSpeed;
                double newY = ViewportLocation.Y - deltaY;

                ViewportLocation = new Point(ViewportLocation.X, Math.Max(0, newY));
            }
        }

        // ==========================================
        // 下面保留你原本写的依赖属性 (Dependency Properties)
        // ==========================================

        public IEnumerable JudgmentLines
        {
            get { return (IEnumerable)GetValue(JudgmentLinesProperty); }
            set { SetValue(JudgmentLinesProperty, value); }
        }

        public static readonly DependencyProperty JudgmentLinesProperty =
            DependencyProperty.Register(
                nameof(JudgmentLines),
                typeof(IEnumerable),
                typeof(TimeLine),
                new PropertyMetadata(null, OnJudgmentLinesChanged));

        private static void OnJudgmentLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimeLine timeLine)
            {
            }
        }

        public double PixelPerTick
        {
            get { return (double)GetValue(PixelPerTickProperty); }
            set { SetValue(PixelPerTickProperty, value); }
        }

        public static readonly DependencyProperty PixelPerTickProperty =
            DependencyProperty.Register(nameof(PixelPerTick), typeof(double), typeof(TimeLine), new PropertyMetadata(100.0));

        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(TimeLine), new PropertyMetadata(1.0));

        public int StartTick
        {
            get { return (int)GetValue(StartTickProperty); }
            set { SetValue(StartTickProperty, value); }
        }

        public static readonly DependencyProperty StartTickProperty =
            DependencyProperty.Register(nameof(StartTick), typeof(int), typeof(TimeLine), new PropertyMetadata(0));

        public int PlayHeadTick
        {
            get { return (int)GetValue(PlayHeadTickProperty); }
            set { SetValue(PlayHeadTickProperty, value); }
        }

        public static readonly DependencyProperty PlayHeadTickProperty =
            DependencyProperty.Register(nameof(PlayHeadTick), typeof(int), typeof(TimeLine), new PropertyMetadata(0));

        public double CameraY
        {
            get { return (double)GetValue(CameraYProperty); }
            set { SetValue(CameraYProperty, value); }
        }

        public static readonly DependencyProperty CameraYProperty =
            DependencyProperty.Register(nameof(CameraY), typeof(double), typeof(TimeLine), new PropertyMetadata(0.0));
    }
}