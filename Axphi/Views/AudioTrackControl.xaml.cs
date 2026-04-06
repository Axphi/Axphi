using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Axphi.Views
{
    public partial class AudioTrackControl : UserControl
    {
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

    }
}