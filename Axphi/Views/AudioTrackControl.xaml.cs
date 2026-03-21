using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Axphi.Views
{
    public partial class AudioTrackControl : UserControl
    {
        // ================= 拖拽核心状态变量 =================
        private Point _layerLastMousePos;
        private double _layerVirtualPixelX;
        private int _lastAppliedTick; // 记录上一帧的 Tick，用来算增量

        public AudioTrackControl()
        {
            InitializeComponent();

            // 1. 注册全局滚动同步
            WeakReferenceMessenger.Default.Register<AudioTrackControl, SyncHorizontalScrollMessage>(this, (recipient, message) =>
            {
                recipient.TrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });

            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        // ================= 滚轮事件透传 (完美适配 Alt 缩放) =================
        private void InnerTrack_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 🌟 如果按下了 Alt（缩放）或 Shift（横向滚动），直接放行给大管家，不要内部消化！
            if (Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Shift) return;

            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            if (sender is UIElement element)
            {
                var parent = VisualTreeHelper.GetParent(element) as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }

        // ================= 音频块完美拖拽 & 吸附 =================
        private void AudioThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // 🌟 取相对于外层绝对静止画板的位置，防抖！
            _layerLastMousePos = Mouse.GetPosition(this);

            if (this.DataContext is AudioTrackViewModel vm)
            {
                _layerVirtualPixelX = vm.LayerPixelXOffset;

                // 记录起步时的准确 Tick
                double exactTick = vm.Timeline.PixelToTick(_layerVirtualPixelX);
                _lastAppliedTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);
            }
            e.Handled = true;
        }

        private void AudioThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDeltaX = currentPos.X - _layerLastMousePos.X;
            _layerLastMousePos = currentPos;

            if (this.DataContext is AudioTrackViewModel vm)
            {
                _layerVirtualPixelX += stableDeltaX;

                double exactTick = vm.Timeline.PixelToTick(_layerVirtualPixelX);
                int snappedTick = vm.Timeline.SnapToClosest(exactTick, isPlayhead: false);

                int stepDelta = snappedTick - _lastAppliedTick;

                if (stepDelta != 0)
                {
                    _lastAppliedTick = snappedTick;
                    vm.Chart.Offset = _lastAppliedTick;

                    // ================= 🌟 核心修复 =================
                    // 只要 Offset 发生了哪怕 1 个 Tick 的变化，
                    // 立刻调用积分器重新计算一次当前的物理宽度，并同步给 UI！
                    vm.LayerPixelWidth = Math.Max(10, vm.Timeline.TickToPixel(vm.AudioDurationTicks));
                    // ===============================================
                }

                // 视觉块的平滑渲染 vs 磁吸渲染
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    vm.LayerPixelXOffset = vm.Timeline.TickToPixel(snappedTick);
                else
                    vm.LayerPixelXOffset = _layerVirtualPixelX;
            }
            e.Handled = true;
        }

        private void AudioThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.DataContext is AudioTrackViewModel vm)
            {
                // 彻底收尾：确保底层和 UI 的 X 坐标和 Width 完美对齐到物理像素上
                vm.Chart.Offset = _lastAppliedTick;

                // 🌟 直接调用 ViewModel 里的统一刷新方法，确保松手时绝对精准！
                vm.UpdatePixels();
            }
            e.Handled = true;
        }
    }
}