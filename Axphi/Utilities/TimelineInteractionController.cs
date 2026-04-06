using Axphi.ViewModels;
using System.Windows;

namespace Axphi.Utilities;

public sealed class TimelineInteractionController
{
    private const double MinimapWidthEpsilon = 0.000001;

    private double _playheadDragMouseOffset;
    private double _workspaceLeftDragOffset;
    private double _workspaceRightDragOffset;
    private bool _isMiddlePanning;
    private Point _middlePanStartMousePos;
    private double _middlePanStartScrollValue;
    private double _minimapViewportStartTick;
    private double _minimapViewportEndTick;

    public double ToAbsolutePointerX(double pointerXOnSurface, double scrollOffset)
    {
        return pointerXOnSurface + scrollOffset;
    }

    public void BeginPlayheadDrag(TimelineViewModel timeline, double absolutePointerX)
    {
        _playheadDragMouseOffset = timeline.PlayheadPositionX - absolutePointerX;
    }

    public double ComputePlayheadSeekSeconds(TimelineViewModel timeline, double absolutePointerX, bool isSnapDragging)
    {
        double targetAbsolutePixel = absolutePointerX;
        if (!isSnapDragging)
        {
            targetAbsolutePixel += _playheadDragMouseOffset;
        }

        targetAbsolutePixel = Clamp(targetAbsolutePixel, 0, timeline.TotalPixelWidth);

        double exactTick = timeline.PixelToTick(targetAbsolutePixel);
        int snappedTick = timeline.SnapToClosest(exactTick, isPlayhead: true);

        return TimeTickConverter.TickToTime(
            snappedTick,
            timeline.CurrentChart.BpmKeyFrames,
            timeline.CurrentChart.InitialBpm);
    }

    public double ComputeRulerSeekSeconds(TimelineViewModel timeline, double absolutePointerX)
    {
        double targetAbsolutePixel = Clamp(absolutePointerX, 0, timeline.TotalPixelWidth);
        double exactTick = timeline.PixelToTick(targetAbsolutePixel);
        int snappedTick = timeline.SnapToClosest(exactTick, isPlayhead: true);

        return TimeTickConverter.TickToTime(
            snappedTick,
            timeline.CurrentChart.BpmKeyFrames,
            timeline.CurrentChart.InitialBpm);
    }

    public void BeginWorkspaceLeftDrag(TimelineViewModel timeline, double absolutePointerX)
    {
        _workspaceLeftDragOffset = timeline.WorkspaceStartX - absolutePointerX;
    }

    public int ComputeWorkspaceStartTick(TimelineViewModel timeline, double absolutePointerX)
    {
        double targetAbsolutePixel = absolutePointerX + _workspaceLeftDragOffset;
        double minDistancePixels = timeline.TickToPixel(1);
        targetAbsolutePixel = Clamp(targetAbsolutePixel, 0, timeline.WorkspaceEndX - minDistancePixels);

        double exactTick = timeline.PixelToTick(targetAbsolutePixel);
        int snappedTick = timeline.SnapToClosest(exactTick, isPlayhead: false);

        return snappedTick >= timeline.WorkspaceEndTick
            ? timeline.WorkspaceEndTick - 1
            : snappedTick;
    }

    public void BeginWorkspaceRightDrag(TimelineViewModel timeline, double absolutePointerX)
    {
        _workspaceRightDragOffset = timeline.WorkspaceEndX - absolutePointerX;
    }

    public int ComputeWorkspaceEndTick(TimelineViewModel timeline, double absolutePointerX)
    {
        double targetAbsolutePixel = absolutePointerX + _workspaceRightDragOffset;
        double minDistancePixels = timeline.TickToPixel(1);

        targetAbsolutePixel = Clamp(
            targetAbsolutePixel,
            timeline.WorkspaceStartX + minDistancePixels,
            timeline.TotalPixelWidth);

        double exactTick = timeline.PixelToTick(targetAbsolutePixel);
        int snappedTick = timeline.SnapToClosest(exactTick, isPlayhead: false);

        return snappedTick <= timeline.WorkspaceStartTick
            ? timeline.WorkspaceStartTick + 1
            : snappedTick;
    }

    public bool BeginMiddlePan(Point mousePos, double scrollValue)
    {
        _isMiddlePanning = true;
        _middlePanStartMousePos = mousePos;
        _middlePanStartScrollValue = scrollValue;
        return true;
    }

    public bool IsMiddlePanning => _isMiddlePanning;

    public double ComputeMiddlePanScroll(Point currentMousePos, double maxScroll)
    {
        double deltaX = currentMousePos.X - _middlePanStartMousePos.X;
        double newScrollValue = _middlePanStartScrollValue - deltaX;
        return Clamp(newScrollValue, 0, maxScroll);
    }

    public void EndMiddlePan()
    {
        _isMiddlePanning = false;
    }

    public double ComputeMinimapPanScroll(TimelineViewModel timeline, double horizontalChange, double currentScroll, double maxScroll)
    {
        if (!TryGetTicksPerMinimapPixel(timeline, out double ticksPerMinimapPixel))
        {
            return Clamp(currentScroll, 0, maxScroll);
        }

        double realPixelDelta = timeline.TickToPixel(horizontalChange * ticksPerMinimapPixel);
        if (!double.IsFinite(realPixelDelta))
        {
            return Clamp(currentScroll, 0, maxScroll);
        }

        return Clamp(currentScroll + realPixelDelta, 0, maxScroll);
    }

    public void BeginMinimapViewportResize(TimelineViewModel timeline)
    {
        _minimapViewportStartTick = timeline.ViewportStartTick;
        _minimapViewportEndTick = timeline.ViewportEndTick;
    }

    public (double StartTick, double EndTick) ComputeMinimapViewportLeftResize(
        TimelineViewModel timeline,
        double horizontalChange,
        double minVisibleTicks)
    {
        if (!TryGetTicksPerMinimapPixel(timeline, out double ticksPerPixel))
        {
            return (_minimapViewportStartTick, _minimapViewportEndTick);
        }

        _minimapViewportStartTick += horizontalChange * ticksPerPixel;

        if (_minimapViewportStartTick > _minimapViewportEndTick - minVisibleTicks)
        {
            _minimapViewportStartTick = _minimapViewportEndTick - minVisibleTicks;
        }

        if (_minimapViewportStartTick < 0)
        {
            _minimapViewportStartTick = 0;
        }

        return (_minimapViewportStartTick, _minimapViewportEndTick);
    }

    public (double StartTick, double EndTick) ComputeMinimapViewportRightResize(
        TimelineViewModel timeline,
        double horizontalChange,
        double minVisibleTicks)
    {
        if (!TryGetTicksPerMinimapPixel(timeline, out double ticksPerPixel))
        {
            return (_minimapViewportStartTick, _minimapViewportEndTick);
        }

        _minimapViewportEndTick += horizontalChange * ticksPerPixel;

        if (_minimapViewportEndTick < _minimapViewportStartTick + minVisibleTicks)
        {
            _minimapViewportEndTick = _minimapViewportStartTick + minVisibleTicks;
        }

        if (_minimapViewportEndTick > timeline.TotalDurationTicks)
        {
            _minimapViewportEndTick = timeline.TotalDurationTicks;
        }

        return (_minimapViewportStartTick, _minimapViewportEndTick);
    }

    public bool TryComputeViewportChangeFromMinimapRange(
        TimelineViewModel timeline,
        double rulerWidth,
        double startTick,
        double endTick,
        out double newZoom,
        out double newOffset,
        out double newMaximum)
    {
        newZoom = timeline.ZoomScale;
        newOffset = 0;
        newMaximum = 0;

        if (!double.IsFinite(rulerWidth) || rulerWidth <= MinimapWidthEpsilon)
        {
            return false;
        }

        double visibleTicks = endTick - startTick;
        if (!double.IsFinite(visibleTicks) || visibleTicks <= MinimapWidthEpsilon)
        {
            return false;
        }

        double basePixelsPerTick = timeline.BasePixelsPerTick;
        if (!double.IsFinite(basePixelsPerTick) || basePixelsPerTick <= MinimapWidthEpsilon)
        {
            return false;
        }

        double rawZoom = rulerWidth / (visibleTicks * basePixelsPerTick);
        if (!double.IsFinite(rawZoom))
        {
            return false;
        }

        newZoom = timeline.ClampZoomScale(rawZoom, timeline.ViewportActualWidth);

        double expectedNewTotalWidth = timeline.TotalDurationTicks * basePixelsPerTick * newZoom;
        if (!double.IsFinite(expectedNewTotalWidth))
        {
            return false;
        }

        newMaximum = Math.Max(0, expectedNewTotalWidth - timeline.ViewportActualWidth + timeline.RightEmptyPadding);

        double computedOffset = startTick * basePixelsPerTick * newZoom;
        if (!double.IsFinite(computedOffset))
        {
            return false;
        }

        newOffset = Clamp(computedOffset, 0, newMaximum);
        return true;
    }

    private static bool TryGetTicksPerMinimapPixel(TimelineViewModel timeline, out double ticksPerPixel)
    {
        ticksPerPixel = 0;

        if (timeline.TotalDurationTicks <= 0)
        {
            return false;
        }

        double minimapWidth = timeline.MinimapActualWidth;
        if (!double.IsFinite(minimapWidth) || minimapWidth <= MinimapWidthEpsilon)
        {
            return false;
        }

        double computed = timeline.TotalDurationTicks / minimapWidth;
        if (!double.IsFinite(computed))
        {
            return false;
        }

        ticksPerPixel = computed;
        return true;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
