using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;
using Axphi.Abstraction;
using NAudio.Utils;
using NAudio.Wave;

namespace Axphi.Playback
{
    /// <summary>
    /// 基于音频播放提供时间同步功能
    /// </summary>
    internal class WasapiOutBasedPlayTimeSyncProvider : IPlayTimeSyncProvider
    {
        private readonly DispatcherTimer _dispatcherTimer;
        private readonly WaveStream _waveStream;
        private readonly WasapiOut _wasapiOut;

        public WasapiOutBasedPlayTimeSyncProvider(WaveStream waveStream, WasapiOut wasapiOut)
        {
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Normal, TickCallback, App.Current.Dispatcher);
            _waveStream = waveStream;
            _wasapiOut = wasapiOut;
        }

        public bool IsRunning => _wasapiOut.PlaybackState == PlaybackState.Playing;

        public TimeSpan Time
        {
            get => _waveStream.CurrentTime;
            set
            {
                _waveStream.CurrentTime = value;
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Updated;

        public void Pause()
        {
            _wasapiOut.Pause();
            _dispatcherTimer.Stop();
        }

        public void Start()
        {
            _wasapiOut.Play();
            _dispatcherTimer.Start();
        }

        public void Stop()
        {
            _waveStream.Seek(0, System.IO.SeekOrigin.Begin);
            _wasapiOut.Stop();
            _dispatcherTimer.Stop();

            Updated?.Invoke(this, EventArgs.Empty);
        }

        private void TickCallback(object? sender, EventArgs e)
        {
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }
}
