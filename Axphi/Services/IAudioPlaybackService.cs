using System;

namespace Axphi.Services;

public interface IAudioPlaybackService : IDisposable
{
    bool HasAudio { get; }
    bool IsPlaying { get; }

    void Load(string fileName);
    void Load(byte[]? audioBytes);
    void StartAtTimelineTime(TimeSpan chartTime);
    void UpdatePlaybackForChartTime(TimeSpan chartTime);
    void Pause();
    void StopAndReset();
    void SeekTo(TimeSpan chartTime);
    void ApplySpeedSettings(double playbackSpeed, bool preserveAudioPitch);
}
