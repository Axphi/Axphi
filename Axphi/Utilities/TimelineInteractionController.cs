using Axphi.ViewModels;
using System.Windows;

namespace Axphi.Utilities;

public sealed class TimelineInteractionController
{
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
        double ticksPerMinimapPixel = timeline.TotalDurationTicks / timeline.MinimapActualWidth;
        double realPixelDelta = timeline.TickToPixel(horizontalChange * ticksPerMinimapPixel);
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
        double ticksPerPixel = timeline.TotalDurationTicks / timeline.MinimapActualWidth;
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
        double ticksPerPixel = timeline.TotalDurationTicks / timeline.MinimapActualWidth;
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
