using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TrackHeaderJudgmentLineViewModel : ObservableObject
    {


        private readonly TrackLayoutService _layoutService;

        public ObservableCollection<string> VisibleProperties { get; } = new();


        [ObservableProperty]
        private JudgementLine _line;



        
        public Point Location => new Point(0, _layoutService.GetYCoordinate(Line));

        


        

        private readonly int _index;

        
        public VectorPropertyViewModel PositionVM { get; }
        public VectorPropertyViewModel ScaleVM { get; }
        public DoublePropertyViewModel RotationVM { get; }
        public DoublePropertyViewModel OpacityVM { get; }
        public DoublePropertyViewModel SpeedVM { get; }

        
        public bool IsExpanded
        {
            get => VisibleProperties.Count > 0;
            set
            {
                if (value)
                {
                    if (VisibleProperties.Count == 0)
                    {
                        VisibleProperties.Add("Position");
                        VisibleProperties.Add("Scale");
                        VisibleProperties.Add("Rotation");
                        VisibleProperties.Add("Opacity");
                        VisibleProperties.Add("Speed");
                    }
                }
                else
                {
                    VisibleProperties.Clear();
                }
            }
        }

        
        public bool IsPositionVisible => VisibleProperties.Contains("Position");
        public bool IsScaleVisible => VisibleProperties.Contains("Scale");
        public bool IsRotationVisible => VisibleProperties.Contains("Rotation");
        public bool IsOpacityVisible => VisibleProperties.Contains("Opacity");
        public bool IsSpeedVisible => VisibleProperties.Contains("Speed");


        // 构造函数：确保在创建时必须传入 JudgementLine
        public TrackHeaderJudgmentLineViewModel(JudgementLine line, int index, TrackLayoutService layoutService)
        {
            _line = line;
            _index = index;
            _layoutService = layoutService;

            VisibleProperties.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(IsPositionVisible));
                OnPropertyChanged(nameof(IsScaleVisible));
                OnPropertyChanged(nameof(IsRotationVisible));
                OnPropertyChanged(nameof(IsOpacityVisible));
                OnPropertyChanged(nameof(IsSpeedVisible));

                // 🌟 核心修改：把 VisibleProperties 这个集合直接传给 Service！
                _layoutService.UpdateActiveProperties(Line, VisibleProperties);
            };

            // 🌟 3. 监听全局排版事件，更新自己的 Location
            _layoutService.LayoutUpdated += OnLayoutUpdated;



            PositionVM = new VectorPropertyViewModel("Position", line.Properties.Position);
            ScaleVM = new VectorPropertyViewModel("Scale", line.Properties.Scale);
            RotationVM = new DoublePropertyViewModel("Rotation", line.Properties.Rotation);
            OpacityVM = new DoublePropertyViewModel("Opacity", line.Properties.Opacity);
            SpeedVM = new DoublePropertyViewModel("Speed", line.Properties.Speed);
            
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            // 排版更新了，通知 UI 重新读取 Location 属性
            OnPropertyChanged(nameof(Location));
        }

        public void Dispose()
        {
            // 良好的习惯：释放事件监听，防止内存泄漏
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
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


        public void ShowSingleProperty(string propertyName)
        {
            VisibleProperties.Clear();
            VisibleProperties.Add(propertyName);
        }
    }
}