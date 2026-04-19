using Axphi.Data;
using Axphi.Data.KeyFrames; // 引入命名空间
using Axphi.Services;
using System;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TimeLineKeyFrameViewModel : TimeLineItemViewModelBase
    {
        public JudgementLine OwnerLine { get; }

        // 🌟 核心修改：使用非泛型接口！
        public IKeyFrame KeyFrameData { get; }

        public string PropertyName { get; }

        private readonly TimeLineViewModel _timelineVM;
        private readonly TrackLayoutService _layoutService;

        // 🌟 完美获取 Tick，完全不需要知道它存的是 Vector 还是 double
        public override int Tick => KeyFrameData.Tick;

        public override Point Location
        {
            get
            {
                double targetY = _layoutService.GetPropertyYCoordinate(OwnerLine, PropertyName);

                if (targetY == -1)
                {
                    IsVisible = false;
                    return new Point(-30, -30);
                }

                IsVisible = true;

                // 🌟 核心计算：让 10x10 的菱形绝对居中
                // 1. X 轴对齐：Tick 的实际像素位置 - 菱形宽度的一半 (5.0)
                double x = (Tick * (_timelineVM.PixelPerTick * _timelineVM.Zoom)) - 5.0;
                // 2. Y 轴对齐：属性行的顶端 Y + 属性行高的一半 - 菱形高度的一半 (5.0)
                double y = targetY + (TrackLayoutService.PropertyRowHeight / 2.0) - 5.0;

                return new Point(x, y);
            }
        }

        // 构造函数接收 IKeyFrame
        public TimeLineKeyFrameViewModel(JudgementLine ownerLine, string propertyName, IKeyFrame keyFrameData, TimeLineViewModel timelineVM, TrackLayoutService layoutService)
        {
            OwnerLine = ownerLine;
            PropertyName = propertyName;
            KeyFrameData = keyFrameData;
            _timelineVM = timelineVM;
            _layoutService = layoutService;

            _layoutService.LayoutUpdated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Location));
        }

        public override void UpdateVisuals()
        {
            OnPropertyChanged(nameof(Location));
        }

        

        public override void Dispose()
        {
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}