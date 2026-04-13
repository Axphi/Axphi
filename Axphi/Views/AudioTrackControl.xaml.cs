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

        public AudioTrackControl()
        {
            InitializeComponent();

            Loaded += (_, _) => ApplyCurrentHorizontalOffset();

            // 1. 注册全局滚动同步
            WeakReferenceMessenger.Default.Register<AudioTrackControl, SyncHorizontalScrollMessage>(this, (recipient, message) =>
            {
                recipient.TrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });

            this.Unloaded += (s, e) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void ApplyCurrentHorizontalOffset()
        {
            if (DataContext is AudioTrackViewModel audioTrackViewModel)
            {
                TrackScrollViewer.ScrollToHorizontalOffset(audioTrackViewModel.Timeline.CurrentHorizontalScrollOffset);
            }
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
            if (this.DataContext is AudioTrackViewModel vm)
            {
                if (vm.IsDragLocked)
                {
                    e.Handled = true;
                    return;
                }

                _layerLastMousePos = Mouse.GetPosition(this);
                vm.OnLayerDragStarted();
            }
            e.Handled = true;
        }

        private void AudioThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (this.DataContext is AudioTrackViewModel lockedVm && lockedVm.IsDragLocked)
            {
                e.Handled = true;
                return;
            }

            Point currentPos = Mouse.GetPosition(this);
            double stableDeltaX = currentPos.X - _layerLastMousePos.X;
            _layerLastMousePos = currentPos;

            if (this.DataContext is AudioTrackViewModel vm)
            {
                vm.OnLayerDragDelta(stableDeltaX);
            }
            e.Handled = true;
        }

        private void AudioThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.DataContext is AudioTrackViewModel vm)
            {
                if (vm.IsDragLocked)
                {
                    e.Handled = true;
                    return;
                }

                vm.OnLayerDragCompleted();
            }
            e.Handled = true;
        }

        private void AudioHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AudioTrackViewModel vm)
            {
                vm.HandleLayerPointerDown();
            }
        }

        private void AudioHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AudioTrackViewModel vm)
            {
                vm.HandleLayerPointerUp();
            }
        }

        private void AudioThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AudioTrackViewModel vm)
            {
                vm.HandleLayerPointerDown();
            }
        }

        private void AudioThumb_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AudioTrackViewModel vm)
            {
                vm.HandleLayerPointerUp();
            }
        }
    }
}