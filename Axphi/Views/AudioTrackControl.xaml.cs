using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Axphi.Views
{
    public partial class AudioTrackControl : UserControl
    {
        private readonly HorizontalDragTracker _layerDragTracker = new();

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
            MouseWheelPassthrough.TryHandle(sender as UIElement, e);
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

                _layerDragTracker.Start(this);
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

            double stableDeltaX = _layerDragTracker.GetDeltaX(this);

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