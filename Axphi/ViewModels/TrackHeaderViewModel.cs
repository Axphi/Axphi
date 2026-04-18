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
        // 私有字段，留着自己用
        private readonly ProjectManager _projectManager;


        
        public ObservableCollection<TrackHeaderJudgmentLineViewModel> LineViewModels { get; } = new();

        public TrackHeaderViewModel(ProjectManager projectManager)
        {
            _projectManager = projectManager;
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;

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
            set => SetProperty(ref _viewportLocation, value);
        }
        private void RefreshLineViewModels()
        {
            LineViewModels.Clear();

            var chart = _projectManager.EditingProject?.Chart;
            if (chart == null || chart.JudgementLines == null) return;

            // 与 Generic.xaml 中 TrackHeaderJudgementLine 的 Height="30" 保持一致
            double itemHeight = 30.0;
            int index = 0;

            foreach (var line in chart.JudgementLines)
            {
                var lineVM = new TrackHeaderJudgmentLineViewModel
                {
                    Line = line,
                    IsExpanded = false, // 默认收起状态
                    // 自动计算坐标：第 0 个是 (0, 0)，第 1 个是 (0, 30)...
                    Location = new Point(0, index * itemHeight)
                };

                LineViewModels.Add(lineVM);
                index++;
            }
        }
    }
}
