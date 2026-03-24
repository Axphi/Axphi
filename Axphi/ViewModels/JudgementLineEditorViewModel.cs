using Axphi.Data;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Axphi.ViewModels
{
    public partial class JudgementLineEditorViewModel : ObservableObject
    {
        private readonly TimelineViewModel _timeline;

        public string[] NoteKindOptions { get; } = Enum.GetNames<NoteKind>();
        public int[] DivisionOptions { get; } = [8, 12, 16, 24, 32];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsVisible))]
        [NotifyPropertyChangedFor(nameof(ActiveTrackName))]
        [NotifyPropertyChangedFor(nameof(PlacementHint))]
        private TrackViewModel? _activeTrack;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlacementHint))]
        private string _currentNoteKind = nameof(NoteKind.Tap);

        [ObservableProperty]
        private int _horizontalDivisions = 16;

        public double HorizontalDivisionsValue
        {
            get => HorizontalDivisions;
            set
            {
                int nextValue = (int)Math.Round(value, MidpointRounding.AwayFromZero);
                nextValue = Math.Clamp(nextValue, 4, 64);
                if (HorizontalDivisions == nextValue)
                {
                    return;
                }

                HorizontalDivisions = nextValue;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        private double _viewZoom = 1.0;

        [ObservableProperty]
        private double _panX;

        [ObservableProperty]
        private double _panY;

        public bool HasHoverPreview { get; private set; }

        public double HoverChartX { get; private set; }

        public int HoverHitTick { get; private set; }

        public bool HasPendingHoldPlacement { get; private set; }

        public double PendingHoldChartX { get; private set; }

        public int PendingHoldStartTick { get; private set; }

        public bool IsVisible => ActiveTrack != null;

        public string ActiveTrackName => ActiveTrack?.TrackName ?? string.Empty;

        public string PlacementHint => HasPendingHoldPlacement
            ? "Hold: 点击结束位置完成放置"
            : CurrentNoteKind == nameof(NoteKind.Hold)
                ? "Hold: 先点起点, 再点终点"
                : "单击放置 Note";

        public TimelineViewModel Timeline => _timeline;

        public JudgementLineEditorViewModel(TimelineViewModel timeline)
        {
            _timeline = timeline;
        }

        public void Open(TrackViewModel track)
        {
            ActiveTrack = track;
            ResetView();
            CancelPendingHoldPlacement();
            ClearHoverPreview();
            track.IsExpanded = true;
        }

        [RelayCommand]
        private void Close()
        {
            CancelPendingHoldPlacement();
            ClearHoverPreview();
            ActiveTrack = null;
        }

        [RelayCommand]
        private void ResetView()
        {
            ViewZoom = 1.0;
            PanX = 0;
            PanY = 0;
        }

        public bool ClearHoverPreview()
        {
            if (!HasHoverPreview)
            {
                return false;
            }

            HasHoverPreview = false;
            return true;
        }

        public bool UpdateHoverPreview(Point viewportPoint, Size viewportSize)
        {
            if (!TryResolvePlacement(viewportPoint, viewportSize, out double chartX, out int hitTick))
            {
                return ClearHoverPreview();
            }

            if (HasPendingHoldPlacement)
            {
                chartX = PendingHoldChartX;
            }

            if (HasHoverPreview && Math.Abs(HoverChartX - chartX) < 0.0001 && HoverHitTick == hitTick)
            {
                return false;
            }

            HoverChartX = chartX;
            HoverHitTick = hitTick;
            HasHoverPreview = true;
            return true;
        }

        public void PanBy(double deltaX, double deltaY)
        {
            PanX += deltaX;
            PanY += deltaY;
        }

        public void ZoomAt(double delta, Point viewportPoint, Size viewportSize)
        {
            double oldZoom = ViewZoom;
            double nextZoom = Math.Clamp(oldZoom * (delta > 0 ? 1.1 : 1.0 / 1.1), 0.4, 4.0);
            if (Math.Abs(nextZoom - oldZoom) < double.Epsilon)
            {
                return;
            }

            double centerX = viewportSize.Width / 2.0;
            double centerY = viewportSize.Height / 2.0;

            double relativeX = viewportPoint.X - centerX - PanX;
            double relativeY = viewportPoint.Y - centerY - PanY;
            double ratio = nextZoom / oldZoom;

            PanX -= relativeX * (ratio - 1.0);
            PanY -= relativeY * (ratio - 1.0);
            ViewZoom = nextZoom;
        }

        public bool ScrollTimeByWheelDelta(int wheelDelta, Size viewportSize)
        {
            if (ActiveTrack == null || wheelDelta == 0)
            {
                return false;
            }

            const double wheelStepPixels = 24.0;
            double pixelsPerSecond = GetCurrentVerticalPixelsPerSecond(viewportSize);
            if (pixelsPerSecond <= double.Epsilon)
            {
                return false;
            }

            double notchDelta = wheelDelta / (double)Mouse.MouseWheelDeltaForOneLine;
            double secondsDelta = notchDelta * wheelStepPixels / pixelsPerSecond;
            if (Math.Abs(secondsDelta) <= double.Epsilon)
            {
                return false;
            }

            double maxSeconds = TimeTickConverter.TickToTime(_timeline.TotalDurationTicks, _timeline.CurrentChart.BpmKeyFrames, _timeline.CurrentChart.InitialBpm);
            double nextSeconds = Math.Clamp(_timeline.CurrentPlayTimeSeconds + secondsDelta, 0, Math.Max(0, maxSeconds));
            if (Math.Abs(nextSeconds - _timeline.CurrentPlayTimeSeconds) <= double.Epsilon)
            {
                return false;
            }

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            _timeline.CurrentPlayTimeSeconds = nextSeconds;
            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(nextSeconds));
            return true;
        }

        public bool TryAddNoteAtPoint(Point viewportPoint, Size viewportSize)
        {
            if (ActiveTrack == null || !Enum.TryParse<NoteKind>(CurrentNoteKind, out var kind))
            {
                return false;
            }

            if (!TryResolvePlacement(viewportPoint, viewportSize, out double snappedChartX, out int hitTick))
            {
                return false;
            }

            if (kind == NoteKind.Hold)
            {
                if (!HasPendingHoldPlacement)
                {
                    PendingHoldChartX = snappedChartX;
                    PendingHoldStartTick = hitTick;
                    HasPendingHoldPlacement = true;
                    HoverChartX = snappedChartX;
                    HoverHitTick = hitTick;
                    HasHoverPreview = true;
                    OnPropertyChanged(nameof(HasPendingHoldPlacement));
                    OnPropertyChanged(nameof(PendingHoldChartX));
                    OnPropertyChanged(nameof(PendingHoldStartTick));
                    OnPropertyChanged(nameof(PlacementHint));
                    return true;
                }

                int holdDuration = Math.Max(1, hitTick - PendingHoldStartTick);
                ActiveTrack.CreateNoteAt(kind, PendingHoldStartTick, PendingHoldChartX, holdDuration);
                HoverChartX = PendingHoldChartX;
                HoverHitTick = hitTick;
                HasHoverPreview = true;
                CancelPendingHoldPlacement();
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
                return true;
            }

            ActiveTrack.CreateNoteAt(kind, hitTick, snappedChartX);
            HoverChartX = snappedChartX;
            HoverHitTick = hitTick;
            HasHoverPreview = true;
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            return true;
        }

        public bool CancelPendingHoldPlacement()
        {
            if (!HasPendingHoldPlacement)
            {
                return false;
            }

            HasPendingHoldPlacement = false;
            PendingHoldChartX = 0;
            PendingHoldStartTick = 0;
            OnPropertyChanged(nameof(HasPendingHoldPlacement));
            OnPropertyChanged(nameof(PendingHoldChartX));
            OnPropertyChanged(nameof(PendingHoldStartTick));
            OnPropertyChanged(nameof(PlacementHint));
            return true;
        }

        private bool TryResolvePlacement(Point viewportPoint, Size viewportSize, out double snappedChartX, out int hitTick)
        {
            snappedChartX = 0;
            hitTick = 0;

            if (ActiveTrack == null)
            {
                return false;
            }

            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(viewportSize, ViewZoom);
            double centerX = viewportSize.Width / 2.0 + PanX;

            double chartX = (viewportPoint.X - centerX) / metrics.PixelsPerChartUnit;
            double divisionWidth = 16.0 / HorizontalDivisions;
            int snappedDivisionIndex = (int)Math.Round((chartX + 8.0) / divisionWidth, MidpointRounding.AwayFromZero);
            snappedDivisionIndex = Math.Clamp(snappedDivisionIndex, 0, HorizontalDivisions);
            snappedChartX = -8.0 + snappedDivisionIndex * divisionWidth;

            double exactTick = JudgementLineEditorRenderMath.ResolveExactTickFromY(_timeline, ActiveTrack, viewportPoint.Y, viewportSize, ViewZoom, PanY);
            hitTick = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? SnapEditorTick(exactTick, viewportPoint.Y, viewportSize)
                : (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);

            return true;
        }

        public int ResolveEditorTick(double viewportY, Size viewportSize, bool snap)
        {
            if (ActiveTrack == null)
            {
                return 0;
            }

            double exactTick = JudgementLineEditorRenderMath.ResolveExactTickFromY(_timeline, ActiveTrack, viewportY, viewportSize, ViewZoom, PanY);
            return snap
                ? SnapEditorTick(exactTick, viewportY, viewportSize)
                : (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);
        }

        private int SnapEditorTick(double exactTick, double viewportY, Size viewportSize)
        {
            if (ActiveTrack == null)
            {
                return (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);
            }

            int rawTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);
            double currentTick = _timeline.GetExactTick();
            double centerY = viewportSize.Height / 2.0 + PanY;
            const double snapThresholdPixels = 14.0;

            int bestTick = rawTick;
            double minPixelDiff = double.MaxValue;

            void TrySnap(int targetTick)
            {
                double y = JudgementLineEditorRenderMath.CalculateHoverY(_timeline, ActiveTrack, currentTick, targetTick, viewportSize, ViewZoom, centerY);
                double pixelDiff = Math.Abs(y - viewportY);
                if (pixelDiff <= snapThresholdPixels && pixelDiff < minPixelDiff)
                {
                    minPixelDiff = pixelDiff;
                    bestTick = targetTick;
                }
            }

            int[] intervals = [128, 64, 32, 16, 8, 4, 2];
            int currentInterval = 128;
            foreach (int interval in intervals)
            {
                double intervalSpacing = Math.Abs(
                    JudgementLineEditorRenderMath.CalculateHoverY(_timeline, ActiveTrack, currentTick, currentTick + interval, viewportSize, ViewZoom, centerY) - centerY);
                if (intervalSpacing >= 20)
                {
                    currentInterval = interval;
                }
                else
                {
                    break;
                }
            }

            TrySnap((int)Math.Round(exactTick / currentInterval) * currentInterval);

            foreach (int targetTick in EnumerateEditorSnapTargets())
            {
                TrySnap(targetTick);
            }

            return bestTick;
        }

        private double GetCurrentVerticalPixelsPerSecond(Size viewportSize)
        {
            if (ActiveTrack == null)
            {
                return 0;
            }

            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(viewportSize, ViewZoom);
            double currentSpeed = ActiveTrack.Data.InitialSpeed;

            EasingUtils.CalculateObjectSingleTransform(
                _timeline.GetExactTick(),
                _timeline.CurrentChart.KeyFrameEasingDirection,
                ActiveTrack.Data.InitialSpeed,
                ActiveTrack.Data.SpeedKeyFrames,
                MathUtils.Lerp,
                out currentSpeed);

            currentSpeed = Math.Max(Math.Abs(currentSpeed), 0.01);
            return metrics.BaseVerticalFlowPixelsPerSecond * currentSpeed;
        }

        private IEnumerable<int> EnumerateEditorSnapTargets()
        {
            foreach (var track in _timeline.Tracks)
            {
                foreach (var kf in track.UIAnchorKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                foreach (var kf in track.UIOffsetKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                foreach (var kf in track.UIScaleKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                foreach (var kf in track.UIRotationKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                foreach (var kf in track.UIOpacityKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                foreach (var kf in track.UISpeedKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;

                foreach (var note in track.UINotes)
                {
                    if (!note.IsSelected)
                    {
                        yield return note.Model.HitTime;
                        if (note.CurrentNoteKind == NoteKind.Hold)
                        {
                            yield return note.Model.HitTime + note.HoldDuration;
                        }
                    }

                    foreach (var kf in note.UIAnchorKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                    foreach (var kf in note.UIOffsetKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                    foreach (var kf in note.UIScaleKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                    foreach (var kf in note.UIRotationKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                    foreach (var kf in note.UIOpacityKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                    foreach (var kf in note.UINoteKindKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
                }
            }

            if (_timeline.BpmTrack != null)
            {
                foreach (var kf in _timeline.BpmTrack.UIBpmKeyframes.Where(kf => !kf.IsSelected)) yield return kf.Model.Time;
            }
        }

        partial void OnCurrentNoteKindChanged(string value)
        {
            if (value != nameof(NoteKind.Hold))
            {
                CancelPendingHoldPlacement();
            }
        }

        partial void OnHorizontalDivisionsChanged(int value)
        {
            OnPropertyChanged(nameof(HorizontalDivisionsValue));
        }

        partial void OnActiveTrackChanged(TrackViewModel? value)
        {
            if (value == null)
            {
                CancelPendingHoldPlacement();
            }
        }
    }
}