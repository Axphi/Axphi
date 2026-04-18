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
    public partial class TrackHeaderViewModel : ObservableObject
    {
        // 私有字段，留着自己用
        private readonly ProjectManager _projectManager;

        
        public IEnumerable<JudgementLine> Lines => _projectManager.EditingProject.Chart.JudgementLines;

        public TrackHeaderViewModel(ProjectManager projectManager)
        {
            _projectManager = projectManager;
            _projectManager.PropertyChanged += OnProjectManagerPropertyChanged;
        }
        private void OnProjectManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_projectManager.EditingProject) || e.PropertyName == "Chart")
            {
                // UI 会重新来读 Lines，直接拿到新工程里的判定线
                OnPropertyChanged(nameof(Lines));
            }
        }


        private Point _viewportLocation;
        public Point ViewportLocation
        {
            get => _viewportLocation;
            set => SetProperty(ref _viewportLocation, value);
        }

    }
}
