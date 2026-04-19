using Axphi.Data;
using Axphi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TimeLineJudgmentLineViewModel : ObservableObject, IDisposable
    {
        // 核心数据
        public JudgementLine Line { get; }

        // 依赖的服务
        private readonly TimeLineViewModel _timelineVM;
        private readonly TrackLayoutService _layoutService;

        // 供 XAML Nodify 绑定的核心属性
        public Point Location => new Point(CalculateX(), _layoutService.GetYCoordinate(Line));
        public double Width => CalculateWidth();
        public double Height => TrackLayoutService.BaseTrackHeight;

        // 构造函数
        public TimeLineJudgmentLineViewModel(JudgementLine line, TimeLineViewModel timelineVM, TrackLayoutService layoutService)
        {
            Line = line;
            _timelineVM = timelineVM;
            _layoutService = layoutService;

            // 监听全局布局事件：如果左侧展开了属性，Y 坐标变了，我也得跟着动
            _layoutService.LayoutUpdated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Location));
        }

        // 当父级的缩放比例改变时，重新计算 X 和 Width
        public void UpdateVisuals()
        {
            OnPropertyChanged(nameof(Location));
            OnPropertyChanged(nameof(Width));
        }

        private double CalculateX()
        {
            return Line.StartTick * (_timelineVM.PixelPerTick * _timelineVM.Zoom);
        }

        private double CalculateWidth()
        {
            return Line.DurationTicks * (_timelineVM.PixelPerTick * _timelineVM.Zoom);
        }

        public void Dispose()
        {
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}