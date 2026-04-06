using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Axphi.Views
{
    public partial class BpmTrackControl : UserControl
    {
        public BpmTrackControl()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            WeakReferenceMessenger.Default.Register<BpmTrackControl, SyncHorizontalScrollMessage>(this, static (recipient, message) =>
            {
                recipient.BpmTrackScrollViewer.ScrollToHorizontalOffset(message.Offset);
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyCurrentHorizontalOffset();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void ApplyCurrentHorizontalOffset()
        {
            if (DataContext is BpmTrackViewModel bpmTrackViewModel)
            {
                BpmTrackScrollViewer.ScrollToHorizontalOffset(bpmTrackViewModel.Timeline.CurrentHorizontalScrollOffset);
            }
        }

        private void InnerTrack_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MouseWheelPassthrough.TryHandle(sender as UIElement, e);
        }

    }
}
