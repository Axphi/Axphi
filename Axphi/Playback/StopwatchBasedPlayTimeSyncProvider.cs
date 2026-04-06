using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using Axphi.Abstraction;

namespace Axphi.Playback
{
    /// <summary>
    /// 基于 Stopwatch 提供时间同步功能
    /// </summary>
    internal class StopwatchBasedPlayTimeSyncProvider : IPlayTimeSyncProvider
    {
        private readonly DispatcherTimer _dispatcherTimer;
        private readonly Stopwatch _stopwatch;
        private TimeSpan _customOffset;


        public StopwatchBasedPlayTimeSyncProvider()
        {
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Normal, TickCallback, App.Current.Dispatcher);
            _stopwatch = new Stopwatch();
        }

        private void TickCallback(object? sender, EventArgs e)
        {
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public TimeSpan Time
        {
            get => _stopwatch.Elapsed + _customOffset;
            set
            {
                _customOffset = value - _stopwatch.Elapsed;
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsRunning => _stopwatch.IsRunning;

        public void Pause()
        {
            _dispatcherTimer.Stop();
            _stopwatch.Stop();
        }

        public void Start()
        {
            _dispatcherTimer.Start();
            _stopwatch.Start();
        }

        public void Stop()
        {
            _customOffset = default;
            _dispatcherTimer.Stop();
            _stopwatch.Stop();
            _stopwatch.Reset();

            Updated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? Updated;
    }
}
