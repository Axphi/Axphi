using Axphi.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Axphi.Views
{
    public partial class ChartDisplay : UserControl
    {
        private ChartDisplayViewModel? _viewModel;

        public ChartDisplay()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Unloaded += OnUnloaded;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.RenderInvalidated -= OnRenderInvalidated;
                _viewModel.UserNotificationRequested -= OnUserNotificationRequested;
            }

            _viewModel = e.NewValue as ChartDisplayViewModel;

            if (_viewModel is not null)
            {
                _viewModel.RenderInvalidated += OnRenderInvalidated;
                _viewModel.UserNotificationRequested += OnUserNotificationRequested;
            }
        }

        private void OnRenderInvalidated(object? sender, EventArgs e)
        {
            InternalChartRenderer.InvalidateVisual();
        }

        private void OnUserNotificationRequested(object? sender, string message)
        {
            MessageBox.Show(message);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.RenderInvalidated -= OnRenderInvalidated;
                _viewModel.UserNotificationRequested -= OnUserNotificationRequested;
                _viewModel = null;
            }
        }
    }
}
