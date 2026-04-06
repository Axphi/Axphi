using Axphi.Data;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Axphi.ViewModels;

public sealed class TimelineSnapService : ITimelineSnapService
{
    private const int SnapshotRefreshIntervalMs = 120;

    private sealed class SnapSnapshot
    {
        public required BpmTrackViewModel? BpmTrack { get; init; }

        public required object TracksIdentity { get; init; }

        public required long BuiltAtMs { get; init; }

        public required int[] AllTicks { get; init; }

        public required int[] UnselectedTicks { get; init; }
    }

    private SnapSnapshot? _snapshot;

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

        var snapshot = GetSnapshot(runtime);
        int lowerTick = (int)Math.Floor(runtime.ExactTickDouble - tickThreshold);
        int upperTick = (int)Math.Ceiling(runtime.ExactTickDouble + tickThreshold);
        var candidates = runtime.IsPlayhead ? snapshot.AllTicks : snapshot.UnselectedTicks;
        QueryRange(candidates, lowerTick, upperTick, TrySnap);

        return bestTick;
    }

    private SnapSnapshot GetSnapshot(TimelineSnapRuntime runtime)
    {
        long nowMs = Environment.TickCount64;
        if (_snapshot != null
            && ReferenceEquals(_snapshot.TracksIdentity, runtime.Tracks)
            && ReferenceEquals(_snapshot.BpmTrack, runtime.BpmTrack)
            && nowMs - _snapshot.BuiltAtMs <= SnapshotRefreshIntervalMs)
        {
            return _snapshot;
        }

        var allTicks = new List<int>(1024);
        var unselectedTicks = new List<int>(1024);

        static void AddTick(List<int> target, int tick)
        {
            target.Add(tick);
        }

        void AddBySelection(bool isSelected, int tick)
        {
            AddTick(allTicks, tick);
            if (!isSelected)
            {
                AddTick(unselectedTicks, tick);
            }
        }

        if (runtime.BpmTrack != null)
        {
            foreach (var keyframe in runtime.BpmTrack.UIBpmKeyframes)
            {
                AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
            }
        }

        foreach (var track in runtime.Tracks)
        {
            foreach (var keyframe in track.UIAnchorKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
            foreach (var keyframe in track.UIOffsetKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
            foreach (var keyframe in track.UIScaleKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
            foreach (var keyframe in track.UIRotationKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
            foreach (var keyframe in track.UIOpacityKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
            foreach (var keyframe in track.UISpeedKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);

            foreach (var note in track.UINotes)
            {
                AddBySelection(note.IsSelected, note.Model.HitTime);
                if (note.CurrentNoteKind == NoteKind.Hold)
                {
                    AddBySelection(note.IsSelected, note.Model.HitTime + note.HoldDuration);
                }

                foreach (var keyframe in note.UIAnchorKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
                foreach (var keyframe in note.UIOffsetKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
                foreach (var keyframe in note.UIScaleKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
                foreach (var keyframe in note.UIRotationKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
                foreach (var keyframe in note.UIOpacityKeyframes) AddBySelection(keyframe.IsSelected, keyframe.Model.Time);

                foreach (var keyframe in note.UINoteKindKeyframes)
                {
                    AddBySelection(keyframe.IsSelected, keyframe.Model.Time);
                }
            }
        }

        _snapshot = new SnapSnapshot
        {
            BpmTrack = runtime.BpmTrack,
            TracksIdentity = runtime.Tracks,
            BuiltAtMs = nowMs,
            AllTicks = BuildSortedDistinctArray(allTicks),
            UnselectedTicks = BuildSortedDistinctArray(unselectedTicks),
        };

        return _snapshot;
    }

    private static int[] BuildSortedDistinctArray(List<int> ticks)
    {
        if (ticks.Count == 0)
        {
            return [];
        }

        ticks.Sort();
        int write = 1;
        for (int read = 1; read < ticks.Count; read++)
        {
            if (ticks[read] != ticks[write - 1])
            {
                ticks[write++] = ticks[read];
            }
        }

        if (write == ticks.Count)
        {
            return ticks.ToArray();
        }

        return ticks.Take(write).ToArray();
    }

    private static void QueryRange(int[] sortedTicks, int lowerInclusive, int upperInclusive, Action<int> onTick)
    {
        if (sortedTicks.Length == 0 || lowerInclusive > upperInclusive)
        {
            return;
        }

        int start = LowerBound(sortedTicks, lowerInclusive);
        for (int index = start; index < sortedTicks.Length; index++)
        {
            int tick = sortedTicks[index];
            if (tick > upperInclusive)
            {
                break;
            }

            onTick(tick);
        }
    }

    private static int LowerBound(int[] sortedTicks, int target)
    {
        int lo = 0;
        int hi = sortedTicks.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (sortedTicks[mid] < target)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
