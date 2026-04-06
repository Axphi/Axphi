using Axphi.Services;
using System;
using System.Windows.Threading;

namespace Axphi.ViewModels
{
    public sealed class TimelineHistoryCoordinator : ITimelineHistoryCoordinator
    {
        private readonly SnapshotHistory<string> _history;
        private readonly DispatcherTimer _commitTimer;

        public TimelineHistoryCoordinator()
        {
            _history = new SnapshotHistory<string>(200);
            _commitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _commitTimer.Tick += OnCommitTimerTick;
        }

        public bool CanUndo => _history.HasPendingChanges || _history.CanUndo;

        public bool CanRedo => _history.CanRedo;

        public void ScheduleSnapshot(string snapshot)
        {
            _history.ObserveSnapshot(snapshot);
            if (_history.HasPendingChanges)
            {
                _commitTimer.Stop();
                _commitTimer.Start();
            }
        }

        public void FlushPending()
        {
            _commitTimer.Stop();
            _history.FlushPendingChanges();
        }

        public void Reset(string snapshot)
        {
            _commitTimer.Stop();
            _history.Reset(snapshot);
        }

        public bool TryUndo(out string snapshot)
        {
            FlushPending();
            return _history.TryUndo(out snapshot);
        }

        public bool TryRedo(out string snapshot)
        {
            FlushPending();
            return _history.TryRedo(out snapshot);
        }

        private void OnCommitTimerTick(object? sender, EventArgs e)
        {
            _commitTimer.Stop();
            _history.FlushPendingChanges();
        }
    }
}
