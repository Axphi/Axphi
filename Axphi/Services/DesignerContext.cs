using System;
using System.Collections.Generic;
using System.Text;
using Axphi.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.Services
{
    public partial class DesignerContext : ObservableObject
    {
        [ObservableProperty]
        private JudgementLine? _selectedJudgementLine;



    }
}
