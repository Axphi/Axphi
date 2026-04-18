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

            
            double itemHeight = 23.0; // 每个条目的高
            int index = 0;

            foreach (var line in chart.JudgementLines)
            {
                var lineVM = new TrackHeaderJudgmentLineViewModel(line, index)
                {
                    IsExpanded = false,
                    Location = new Point(0, index * itemHeight)
                };

                LineViewModels.Add(lineVM);
                index++;
            }
        }
    }
}
