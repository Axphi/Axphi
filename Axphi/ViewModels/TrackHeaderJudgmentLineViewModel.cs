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


        private readonly int _index;

        // 构造函数：确保在创建时必须传入 JudgementLine
        public TrackHeaderJudgmentLineViewModel(JudgementLine line, int index)
        {
            _line = line;
            _index = index;
        }


        public string DisplayName
        {
            get
            {
                if (Line.Name == null)
                {
                    return $"Line {_index}";
                }
                return Line.Name;
            }
            set
            {
                // 当用户在前端修改名字时，保存回底层模型
                if (Line.Name != value)
                {
                    Line.Name = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }
    }   
}
