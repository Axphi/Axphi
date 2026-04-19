using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Text;
using System.Windows.Media;

namespace Axphi.ViewModels
{
    public class TrackHeaderViewModel : ObservableObject
    {
        
        private readonly ProjectManager _projectManager;


        private readonly TrackLayoutService _layoutService;


        public ObservableCollection<TrackHeaderJudgmentLineViewModel> LineViewModels { get; } = new();

        public TrackHeaderViewModel(ProjectManager projectManager, TrackLayoutService layoutService)
        {
            _projectManager = projectManager;
            _layoutService = layoutService;
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;


            // 监听服务的相机同步广播
            _layoutService.ViewportYChanged += (sender, newY) =>
            {
                // 核心防抖：如果广播是我自己发出的，我就不处理，防止死循环
                if (sender != this && Math.Abs(_viewportLocation.Y - newY) > 0.01)
                {
                    // 左侧的 X 永远是 0
                    ViewportLocation = new Point(0, newY);
                }
            };


            RefreshLineViewModels();
        }
        private void OnProjectManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_projectManager.EditingProject) || e.PropertyName == "Chart")
            {
                // 当工程或谱面发生变化时，重新生成 VM 列表
                RefreshLineViewModels();
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
        private void RefreshLineViewModels() 
        {
            LineViewModels.Clear();

            var chart = _projectManager.EditingProject?.Chart;
            if (chart == null || chart.JudgementLines == null) return;



            int index = 0;

            foreach (var line in chart.JudgementLines)
            {
                var lineVM = new TrackHeaderJudgmentLineViewModel(line, index, _layoutService);


                LineViewModels.Add(lineVM);
                index++;
            }
            // 调用 InitializeLines，并且传入底层的 chart.JudgementLines！
            _layoutService.InitializeLines(chart.JudgementLines);
        }
    }
}
