using System;
using System.Collections.Generic;
using System.Text;
using Axphi.Abstraction;
using Axphi.Playback;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.Services
{
    public class PlaybackService : ObservableObject
    {
        private readonly IPlayTimeSyncProvider _defaultPlayTimeSyncProvider =
            new StopwatchBasedPlayTimeSyncProvider();
        private IPlayTimeSyncProvider? _customPlayTimeSyncProvider;

        public IPlayTimeSyncProvider? CustomPlayTimeSyncProvider
        {
            get => _customPlayTimeSyncProvider;
            set
            {
                if (ReferenceEquals(_customPlayTimeSyncProvider, value))
                {
                    return;
                }

                if (_customPlayTimeSyncProvider is { })
                {
                    _customPlayTimeSyncProvider.Updated -= PlayTimeSyncProviderUpdated;
                }

                // sync time
                if (value is { } newValue)
                {
                    newValue.Time = _customPlayTimeSyncProvider?.Time ?? _defaultPlayTimeSyncProvider.Time;
                    newValue.Updated += PlayTimeSyncProviderUpdated;
                    _defaultPlayTimeSyncProvider.Updated -= PlayTimeSyncProviderUpdated;
                }
                else
                {
                    _defaultPlayTimeSyncProvider.Time = _customPlayTimeSyncProvider!.Time;
                    _defaultPlayTimeSyncProvider.Updated += PlayTimeSyncProviderUpdated;
                }

                _customPlayTimeSyncProvider = value;
            }
        }
        public IChartRenderer? ChartRenderer { get; set; }

        public PlaybackService()
        {
            _defaultPlayTimeSyncProvider.Updated += PlayTimeSyncProviderUpdated;
        }

        public void Play()
        {
            var syncProvider = _customPlayTimeSyncProvider ?? _defaultPlayTimeSyncProvider;
            syncProvider.Start();
        }

        public void Stop()
        {
            var syncProvider = _customPlayTimeSyncProvider ?? _defaultPlayTimeSyncProvider;
            syncProvider.Stop();
        }

        public void Pause()
        {
            var syncProvider = _customPlayTimeSyncProvider ?? _defaultPlayTimeSyncProvider;
            syncProvider.Pause();
        }

        public bool IsPlaying => (_customPlayTimeSyncProvider ?? _defaultPlayTimeSyncProvider).IsRunning;

        public bool PauseWhenSettingTime { get; set; } = true;

        public TimeSpan Time
        {
            get => (_customPlayTimeSyncProvider ?? _defaultPlayTimeSyncProvider).Time;
            set
            {
                var syncProvider = _customPlayTimeSyncProvider ?? _defaultPlayTimeSyncProvider;
                if (PauseWhenSettingTime)
                {
                    syncProvider.Pause();
                }

                syncProvider.Time = value;
            }
        }

        private void PlayTimeSyncProviderUpdated(object? sender, EventArgs e)
        {
            var time = ((IPlayTimeSyncProvider)sender!).Time;
            if (ChartRenderer is { } chartRenderer)
            {
                chartRenderer.Time = time;
            }

            OnPropertyChanged(nameof(Time));
        }
    }
}
