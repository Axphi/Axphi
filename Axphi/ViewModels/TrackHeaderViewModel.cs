using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TrackHeaderViewModel : ObservableObject, IDisposable
    {
        
        private readonly ProjectManager _projectManager;


        private readonly TrackLayoutService _layoutService;

        public TrackHeaderViewModel(ProjectManager projectManager, TrackLayoutService layoutService)
        {
            _projectManager = projectManager;
            _layoutService = layoutService;
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;


            // 监听服务的相机同步广播
            _layoutService.ViewportYChanged += OnViewportYChanged;
            _layoutService.LayoutUpdated += OnLayoutUpdated;

            InitializeLayout();
        }

        public Chart? CurrentChart => _projectManager.EditingProject?.Chart;
        public ITrackLayoutProvider LayoutProvider => _layoutService;

        [ObservableProperty]
        private int _layoutVersion;

        private void OnProjectManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_projectManager.EditingProject) || e.PropertyName == "Chart")
            {
                OnPropertyChanged(nameof(CurrentChart));
                InitializeLayout();
            }
        }

        private void OnViewportYChanged(object? sender, double newY)
        {
            // 核心防抖：如果广播是我自己发出的，我就不处理，防止死循环
            if (sender != this && Math.Abs(_viewportLocation.Y - newY) > 0.01)
            {
                // 左侧的 X 永远是 0
                ViewportLocation = new Point(0, newY);
            }
        }


        private Point _viewportLocation;
        public Point ViewportLocation
        {
            get => _viewportLocation;
            set
            {
                // 如果 UI 拖拽导致值改变了，不仅要更新自己，还要通知服务
                if (SetProperty(ref _viewportLocation, value))
                {
                    _layoutService.UpdateViewportY(value.Y, this);
                }
            }
        }

        public double ContentHeight => _layoutService.ContentHeight;

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(ContentHeight));
            LayoutVersion++;
        }

        public double GetLineYCoordinate(JudgementLine line)
        {
            return _layoutService.GetYCoordinate(line);
        }

        public IReadOnlyList<string> GetActiveProperties(JudgementLine line)
        {
            return _layoutService.GetActiveProperties(line);
        }

        public bool IsLineExpanded(JudgementLine line)
        {
            return _layoutService.IsLineExpanded(line);
        }

        public void ToggleLineExpanded(JudgementLine line)
        {
            _layoutService.ToggleLineExpanded(line);
        }

        private void InitializeLayout()
        {
            var chart = _projectManager.EditingProject?.Chart;
            _layoutService.InitializeLines(chart?.JudgementLines ?? []);
        }

        public void Dispose()
        {
            _projectManager.PropertyChanged -= OnProjectManagerPropertyChanged;
            _layoutService.ViewportYChanged -= OnViewportYChanged;
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}
