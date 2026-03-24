using Axphi.Data;
using Axphi.ViewModels;
using System;
using System.Windows;

namespace Axphi.Utilities
{
    internal static class JudgementLineEditorRenderMath
    {
        private const double BaseVerticalFlowPixelsPerSecondAt1080 = 648.0;
        private const double BaseNoteChartWidth = 1.95;

        internal readonly record struct ViewMetrics(
            double ClientWidth,
            double ClientHeight,
            double PixelsPerChartUnit,
            double BaseVerticalFlowPixelsPerSecond,
            double NotePixelWidth);

        public static ViewMetrics CalculateMetrics(Size viewportSize, double viewZoom)
        {
            double clientWidth = viewportSize.Width;
            double clientHeight = clientWidth / 16.0 * 9.0;
            if (clientHeight > viewportSize.Height)
            {
                clientHeight = viewportSize.Height;
                clientWidth = clientHeight / 9.0 * 16.0;
            }

            double pixelsPerChartUnit = (clientWidth / 16.0) * viewZoom;
            double baseVerticalFlowPixelsPerSecond = BaseVerticalFlowPixelsPerSecondAt1080 * (clientHeight / 1080.0) * viewZoom;
            double notePixelWidth = BaseNoteChartWidth * pixelsPerChartUnit;

            return new ViewMetrics(clientWidth, clientHeight, pixelsPerChartUnit, baseVerticalFlowPixelsPerSecond, notePixelWidth);
        }

        public static double CalculateNoteY(TimelineViewModel timeline, TrackViewModel track, NoteViewModel note, double currentTick, Size viewportSize, double viewZoom, double centerY)
        {
            var metrics = CalculateMetrics(viewportSize, viewZoom);
            double noteSpeedMultiplier = note.Model.CustomSpeed ?? 1.0;
            double pixelDistance = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, note.HitTime, noteSpeedMultiplier, metrics);
            double pixelOffsetY = note.CurrentOffsetY * metrics.PixelsPerChartUnit;
            return centerY + pixelOffsetY + pixelDistance;
        }

        public static double CalculateHoverY(TimelineViewModel timeline, TrackViewModel track, double currentTick, double hitTick, Size viewportSize, double viewZoom, double centerY)
        {
            var metrics = CalculateMetrics(viewportSize, viewZoom);
            double pixelDistance = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, hitTick, 1.0, metrics);
            return centerY + pixelDistance;
        }

        public static double CalculateHoldLength(TimelineViewModel timeline, TrackViewModel track, NoteViewModel note, Size viewportSize, double viewZoom)
        {
            var metrics = CalculateMetrics(viewportSize, viewZoom);
            double noteSpeedMultiplier = note.Model.CustomSpeed ?? 1.0;

            if (track.Data.SpeedMode == "Realtime")
            {
                double currentTick = timeline.GetExactTick();
                double holdStartSeconds = TimeTickConverter.TickToTime(note.HitTime, timeline.CurrentChart.BpmKeyFrames, timeline.CurrentChart.InitialBpm);
                double holdEndSeconds = TimeTickConverter.TickToTime(note.HitTime + note.HoldDuration, timeline.CurrentChart.BpmKeyFrames, timeline.CurrentChart.InitialBpm);
                double holdDurationSeconds = Math.Max(0, holdEndSeconds - holdStartSeconds);

                double currentRealtimeSpeed = track.Data.InitialSpeed;
                EasingUtils.CalculateObjectSingleTransform(
                    currentTick,
                    timeline.CurrentChart.KeyFrameEasingDirection,
                    track.Data.InitialSpeed,
                    track.Data.SpeedKeyFrames,
                    MathUtils.Lerp,
                    out currentRealtimeSpeed);

                double actualPixelsPerSecond = metrics.BaseVerticalFlowPixelsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier;
                return Math.Abs(actualPixelsPerSecond * holdDurationSeconds);
            }

            return Math.Abs(CalculateTravelDistance(timeline.CurrentChart, track.Data, note.HitTime, note.HitTime + note.HoldDuration, noteSpeedMultiplier, metrics));
        }

        public static int ResolveHitTickFromY(TimelineViewModel timeline, TrackViewModel track, double viewportY, Size viewportSize, double viewZoom, double panY)
        {
            return (int)Math.Round(ResolveExactTickFromY(timeline, track, viewportY, viewportSize, viewZoom, panY), MidpointRounding.AwayFromZero);
        }

        public static double ResolveExactTickFromY(TimelineViewModel timeline, TrackViewModel track, double viewportY, Size viewportSize, double viewZoom, double panY)
        {
            double centerY = viewportSize.Height / 2.0 + panY;
            double targetOffset = viewportY - centerY;
            double currentTick = timeline.GetExactTick();
            int startTick = Math.Max(track.Data.StartTick, 0);
            int endTick = Math.Max(startTick, track.Data.StartTick + track.Data.DurationTicks);

            if (targetOffset >= 0)
            {
                return currentTick;
            }

            double startOffset = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, startTick, 1.0, CalculateMetrics(viewportSize, viewZoom));
            double endOffset = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, endTick, 1.0, CalculateMetrics(viewportSize, viewZoom));

            if (targetOffset >= startOffset)
            {
                return startTick;
            }

            if (targetOffset <= endOffset)
            {
                return endTick;
            }

            int low = startTick;
            int high = endTick;
            var metrics = CalculateMetrics(viewportSize, viewZoom);
            while (high - low > 1)
            {
                int mid = low + ((high - low) / 2);
                double midOffset = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, mid, 1.0, metrics);

                if (midOffset > targetOffset)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            double lowOffset = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, low, 1.0, metrics);
            double highOffset = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, high, 1.0, metrics);
            double offsetSpan = highOffset - lowOffset;
            if (Math.Abs(offsetSpan) < double.Epsilon)
            {
                return Math.Abs(lowOffset - targetOffset) <= Math.Abs(highOffset - targetOffset) ? low : high;
            }

            double interpolation = (targetOffset - lowOffset) / offsetSpan;
            interpolation = Math.Clamp(interpolation, 0.0, 1.0);
            return low + ((high - low) * interpolation);
        }

        private static double CalculateTravelDistance(Chart chart, JudgementLine line, double startTick, double endTick, double noteSpeedMultiplier, ViewMetrics metrics)
        {
            if (line.SpeedMode == "Realtime")
            {
                double currentSeconds = TimeTickConverter.TickToTime(startTick, chart.BpmKeyFrames, chart.InitialBpm);
                double hitTimeSeconds = TimeTickConverter.TickToTime(endTick, chart.BpmKeyFrames, chart.InitialBpm);
                double currentRealtimeSpeed = line.InitialSpeed;

                EasingUtils.CalculateObjectSingleTransform(
                    startTick,
                    chart.KeyFrameEasingDirection,
                    line.InitialSpeed,
                    line.SpeedKeyFrames,
                    MathUtils.Lerp,
                    out currentRealtimeSpeed);

                double actualPixelsPerSecond = metrics.BaseVerticalFlowPixelsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier;
                return -actualPixelsPerSecond * (hitTimeSeconds - currentSeconds);
            }

            return -CalculateIntegralDistance(startTick, endTick, line, chart, noteSpeedMultiplier, metrics.BaseVerticalFlowPixelsPerSecond);
        }

        private static double CalculateIntegralDistance(double startTick, double endTick, JudgementLine line, Chart chart, double noteSpeedMultiplier, double baseVerticalFlowPixelsPerSecond)
        {
            if (Math.Abs(startTick - endTick) < double.Epsilon)
            {
                return 0;
            }

            int steps = 150;
            double totalDistance = 0;
            double tMin = Math.Min(startTick, endTick);
            double tMax = Math.Max(startTick, endTick);
            double stepTick = (double)(tMax - tMin) / steps;

            for (int index = 0; index < steps; index++)
            {
                double t1 = tMin + index * stepTick;
                double t2 = tMin + (index + 1) * stepTick;

                double sec1 = TimeTickConverter.TickToTime(t1, chart.BpmKeyFrames, chart.InitialBpm);
                double sec2 = TimeTickConverter.TickToTime(t2, chart.BpmKeyFrames, chart.InitialBpm);
                double midTick = (t1 + t2) / 2.0;

                EasingUtils.CalculateObjectSingleTransform(
                    midTick,
                    chart.KeyFrameEasingDirection,
                    line.InitialSpeed,
                    line.SpeedKeyFrames,
                    MathUtils.Lerp,
                    out var midSpeed);

                totalDistance += midSpeed * (sec2 - sec1);
            }

            double pixelDistance = totalDistance * baseVerticalFlowPixelsPerSecond * noteSpeedMultiplier;
            return startTick <= endTick ? pixelDistance : -pixelDistance;
        }
    }
}