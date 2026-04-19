using Axphi.Data;
using Axphi.Services;
using System;
using System.Windows;

namespace Axphi.ViewModels
{
    // 🌟 继承基类
    public partial class TimeLineJudgmentLineViewModel : TimeLineItemViewModelBase
    {
        public JudgementLine Line { get; }
        private readonly TimeLineViewModel _timelineVM;
        private readonly TrackLayoutService _layoutService;

        // 🌟 实现抽象属性
        public override int Tick => Line.StartTick;
        public override Point Location => new Point(CalculateX(), _layoutService.GetYCoordinate(Line));

        public double Width => CalculateWidth();
        public double Height => TrackLayoutService.BaseTrackHeight;

        public TimeLineJudgmentLineViewModel(JudgementLine line, TimeLineViewModel timelineVM, TrackLayoutService layoutService)
        {
            Line = line;
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
            OnPropertyChanged(nameof(Width));
        }

        private double CalculateX() => Tick * (_timelineVM.PixelPerTick * _timelineVM.Zoom);
        private double CalculateWidth() => Line.DurationTicks * (_timelineVM.PixelPerTick * _timelineVM.Zoom);

        public override void Dispose()
        {
            _layoutService.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}