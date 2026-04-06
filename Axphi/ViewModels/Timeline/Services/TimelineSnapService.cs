using Axphi.Data;
using System;

namespace Axphi.ViewModels;

public sealed class TimelineSnapService : ITimelineSnapService
{
    public int ResolveSnappedTick(TimelineSnapRuntime runtime)
    {
        int rawTick = (int)Math.Round(runtime.ExactTickDouble, MidpointRounding.AwayFromZero);

        if (!runtime.IsSnapModifierActive)
        {
            return rawTick;
        }

        if (runtime.PixelsPerTick <= 0)
        {
            return rawTick;
        }

        const int snapThresholdPixels = 12;
        double tickThreshold = snapThresholdPixels / runtime.PixelsPerTick;

        int bestTick = rawTick;
        double minDiff = double.MaxValue;

        int[] intervals = [128, 64, 32, 16, 8, 4, 2];
        int currentInterval = 128;

        foreach (var interval in intervals)
        {
            if (interval * runtime.PixelsPerTick >= 20)
            {
                currentInterval = interval;
            }
            else
            {
                break;
            }
        }

        int gridTick = (int)Math.Round(runtime.ExactTickDouble / currentInterval, MidpointRounding.AwayFromZero) * currentInterval;
        double gridDiff = Math.Abs(gridTick - runtime.ExactTickDouble);
        if (gridDiff <= tickThreshold)
        {
            bestTick = gridTick;
            minDiff = gridDiff;
        }

        void TrySnap(int targetTick)
        {
            double diff = Math.Abs(targetTick - runtime.ExactTickDouble);
            if (diff <= tickThreshold && diff < minDiff)
            {
                minDiff = diff;
                bestTick = targetTick;
            }
        }

        if (!runtime.IsPlayhead)
        {
            TrySnap(runtime.PlayheadTick);
        }

        bool ShouldIgnore(bool isSelected) => !runtime.IsPlayhead && isSelected;

        if (runtime.BpmTrack != null)
        {
            foreach (var keyframe in runtime.BpmTrack.UIBpmKeyframes)
            {
                if (!ShouldIgnore(keyframe.IsSelected))
                {
                    TrySnap(keyframe.Model.Time);
                }
            }
        }

        foreach (var track in runtime.Tracks)
        {
            foreach (var keyframe in track.UIAnchorKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
            foreach (var keyframe in track.UIOffsetKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
            foreach (var keyframe in track.UIScaleKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
            foreach (var keyframe in track.UIRotationKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
            foreach (var keyframe in track.UIOpacityKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
            foreach (var keyframe in track.UISpeedKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);

            foreach (var note in track.UINotes)
            {
                if (!ShouldIgnore(note.IsSelected))
                {
                    TrySnap(note.Model.HitTime);
                    if (note.CurrentNoteKind == NoteKind.Hold)
                    {
                        TrySnap(note.Model.HitTime + note.HoldDuration);
                    }
                }

                foreach (var keyframe in note.UIAnchorKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
                foreach (var keyframe in note.UIOffsetKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
                foreach (var keyframe in note.UIScaleKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
                foreach (var keyframe in note.UIRotationKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);
                foreach (var keyframe in note.UIOpacityKeyframes) if (!ShouldIgnore(keyframe.IsSelected)) TrySnap(keyframe.Model.Time);

                if (note.UINoteKindKeyframes != null)
                {
                    foreach (var keyframe in note.UINoteKindKeyframes)
                    {
                        if (!ShouldIgnore(keyframe.IsSelected))
                        {
                            TrySnap(keyframe.Model.Time);
                        }
                    }
                }
            }
        }

        return bestTick;
    }
}
