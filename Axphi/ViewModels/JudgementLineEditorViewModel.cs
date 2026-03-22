using Axphi.Data;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
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
        private TrackViewModel? _activeTrack;

        [ObservableProperty]
        private string _currentNoteKind = nameof(NoteKind.Tap);

        [ObservableProperty]
        private int _horizontalDivisions = 16;

        [ObservableProperty]
        private double _viewZoom = 1.0;

        [ObservableProperty]
        private double _panX;

        [ObservableProperty]
        private double _panY;

        public bool HasHoverPreview { get; private set; }

        public double HoverChartX { get; private set; }

        public int HoverHitTick { get; private set; }

        public bool IsVisible => ActiveTrack != null;

        public string ActiveTrackName => ActiveTrack?.TrackName ?? string.Empty;

        public TimelineViewModel Timeline => _timeline;

        public JudgementLineEditorViewModel(TimelineViewModel timeline)
        {
            _timeline = timeline;
        }

        public void Open(TrackViewModel track)
        {
            ActiveTrack = track;
            ResetView();
            ClearHoverPreview();
            track.IsExpanded = true;
        }

        [RelayCommand]
        private void Close()
        {
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

            ActiveTrack.CreateNoteAt(kind, hitTick, snappedChartX);
            HoverChartX = snappedChartX;
            HoverHitTick = hitTick;
            HasHoverPreview = true;
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
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
            snappedChartX = Math.Round(chartX / divisionWidth, MidpointRounding.AwayFromZero) * divisionWidth;
            snappedChartX = Math.Clamp(snappedChartX, -8.0, 8.0);

            hitTick = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? _timeline.SnapToClosest(JudgementLineEditorRenderMath.ResolveHitTickFromY(_timeline, ActiveTrack, viewportPoint.Y, viewportSize, ViewZoom, PanY), isPlayhead: false)
                : JudgementLineEditorRenderMath.ResolveHitTickFromY(_timeline, ActiveTrack, viewportPoint.Y, viewportSize, ViewZoom, PanY);

            return true;
        }
    }
}