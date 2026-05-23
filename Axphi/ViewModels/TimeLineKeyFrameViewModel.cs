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

        public IKeyFrame KeyFrameData { get; }

        public string PropertyName { get; }

        
        private readonly TrackLayoutService _layoutService;

        
        private int _tick;
        public override int Tick
        {
            get => KeyFrameData.Tick;
            set
            {
                if (KeyFrameData.Tick != value)
                {
                    KeyFrameData.Tick = value;
                    UpdateVisuals();          // 重新计算 Location
                    OnPropertyChanged(nameof(Tick));
                }
            }
        }

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

                // 让 10x10 的菱形绝对居中
                // X 轴对齐：Tick 的实际像素位置 - 菱形宽度的一半 (5.0)
                double x = (Tick * (PixelPerTick * Zoom)) - 5.0;
                // Y 轴对齐：属性行的顶端 Y + 属性行高的一半 - 菱形高度的一半 (5.0)
                double y = targetY + (TrackLayoutService.PropertyRowHeight / 2.0) - 5.0;

                return new Point(x, y);
            }
        }

        
        public TimeLineKeyFrameViewModel(
            JudgementLine ownerLine, 
            string propertyName, 
            IKeyFrame keyFrameData, 
            TrackLayoutService layoutService, 
            double pixelPerTick, 
            double zoom)
        {
            OwnerLine = ownerLine;
            PropertyName = propertyName;
            KeyFrameData = keyFrameData;
            
            _layoutService = layoutService;

            // 初始化布局参数
            PixelPerTick = pixelPerTick;
            Zoom = zoom;

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