using Axphi.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TrackHeaderJudgmentLineViewModel : ObservableObject
    {
        [ObservableProperty]
        private JudgementLine _line;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private Point _location;
    }   
}
