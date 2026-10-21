using NAudio.Wave;
using System;
using System.IO;

namespace Axphi.Services;

public sealed class AudioPlaybackService : IAudioPlaybackService
{
    private const int WasapiLatencyMilliseconds = 50;

    private AudioFileReader? _musicReader;
    private WasapiOut? _wasapiOut;
    private string? _temporaryAudioFilePath;
    private string? _temporaryDecodedAudioFilePath;

    public bool HasAudio => _musicReader is not null;
    public bool IsPlaying => _wasapiOut?.PlaybackState == PlaybackState.Playing;

    public void Load(string fileName)
    {
        ResetLoadedAudio();
        LoadAudioFromFile(fileName, isTemporaryFile: false);
    }

    public void Load(byte[]? audioBytes)
    {
        ResetLoadedAudio();

        if (audioBytes is not { Length: > 0 })
        {
            return;
        }

        string extension = GetAudioFileExtension(audioBytes);
        string temporaryAudioFilePath = Path.Combine(Path.GetTempPath(), $"axphi-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(temporaryAudioFilePath, audioBytes);

        LoadAudioFromFile(temporaryAudioFilePath, isTemporaryFile: true);
    }

    public void StartAtTimelineTime(TimeSpan chartTime)
    {
        if (_musicReader == null)
        {
            return;
        }

        if (!TryGetTargetAudioTime(chartTime, out TimeSpan audioTime))
        {
            Pause();
            return;
        }

        _wasapiOut?.Stop();
        _musicReader.CurrentTime = audioTime;
        _wasapiOut?.Play();
    }

    public void UpdatePlaybackForChartTime(TimeSpan chartTime)
    {
        if (_wasapiOut == null || _musicReader == null)
        {
            return;
        }

        _musicReader.Volume = 1.0f;

        if (TryGetTargetAudioTime(chartTime, out _))
        {
            if (_wasapiOut.PlaybackState != PlaybackState.Playing)
            {
                StartAtTimelineTime(chartTime);
            }
        }
        else if (_wasapiOut.PlaybackState == PlaybackState.Playing)
        {
            Pause();
        }
    }

    public void Pause()
    {
        _wasapiOut?.Pause();
    }

    public void StopAndReset()
    {
        _wasapiOut?.Stop();

        if (_musicReader != null)
        {
            _musicReader.Position = 0;
        }
    }

    public void SeekTo(TimeSpan chartTime)
    {
        if (_musicReader == null)
        {
            return;
        }

        _musicReader.CurrentTime = TryGetTargetAudioTime(chartTime, out TimeSpan audioTime)
            ? audioTime
            : TimeSpan.Zero;
    }

    public void ApplySpeedSettings(double playbackSpeed, bool preserveAudioPitch)
    {
        // 当前音频链路仍使用 AudioFileReader + WasapiOut，暂不支持实时变速。
        // 保留统一入口，后续可在这里切换到 SoundTouch sample provider。
    }

    public void Dispose()
    {
        ResetLoadedAudio();
    }

    private void LoadAudioFromFile(string fileName, bool isTemporaryFile)
    {
        try
        {
            string playbackFilePath = PreparePlaybackFile(fileName);
            _musicReader = new AudioFileReader(playbackFilePath);
            RecreateAudioOutput();

            if (isTemporaryFile)
            {
                _temporaryAudioFilePath = fileName;
            }
        }
        catch
        {
            DeleteTemporaryDecodedAudioFile();

            if (isTemporaryFile && File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            throw;
        }
    }

    private void ResetLoadedAudio()
    {
        _wasapiOut?.Stop();
        _wasapiOut?.Dispose();
        _wasapiOut = null;

        _musicReader?.Dispose();
        _musicReader = null;

        DeleteTemporaryAudioFile();
        DeleteTemporaryDecodedAudioFile();
    }

    private void RecreateAudioOutput()
    {
        if (_musicReader == null)
        {
            _wasapiOut?.Dispose();
            _wasapiOut = null;
            return;
        }

        _wasapiOut?.Stop();
        _wasapiOut?.Dispose();
        _wasapiOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, WasapiLatencyMilliseconds);
        _wasapiOut.Init(_musicReader);
    }

    private string PreparePlaybackFile(string sourceFilePath)
    {
        DeleteTemporaryDecodedAudioFile();

        if (string.Equals(Path.GetExtension(sourceFilePath), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            return sourceFilePath;
        }

        string decodedFilePath = Path.Combine(Path.GetTempPath(), $"axphi-decoded-{Guid.NewGuid():N}.wav");
        using (var reader = new AudioFileReader(sourceFilePath))
        {
            WaveFileWriter.CreateWaveFile16(decodedFilePath, reader);
        }

        _temporaryDecodedAudioFilePath = decodedFilePath;
        return decodedFilePath;
    }

    private bool TryGetTargetAudioTime(TimeSpan chartTime, out TimeSpan audioTime)
    {
        audioTime = TimeSpan.Zero;

        if (_musicReader == null)
        {
            return false;
        }

        double targetAudioSeconds = chartTime.TotalSeconds;

        if (targetAudioSeconds < 0 || targetAudioSeconds >= _musicReader.TotalTime.TotalSeconds)
        {
            return false;
        }

        audioTime = TimeSpan.FromSeconds(targetAudioSeconds);
        return true;
    }

    private void DeleteTemporaryAudioFile()
    {
        if (string.IsNullOrWhiteSpace(_temporaryAudioFilePath))
        {
            return;
        }

        if (File.Exists(_temporaryAudioFilePath))
        {
            File.Delete(_temporaryAudioFilePath);
        }

        _temporaryAudioFilePath = null;
    }

    private void DeleteTemporaryDecodedAudioFile()
    {
        if (string.IsNullOrWhiteSpace(_temporaryDecodedAudioFilePath))
        {
            return;
        }

        if (File.Exists(_temporaryDecodedAudioFilePath))
        {
            File.Delete(_temporaryDecodedAudioFilePath);
        }

        _temporaryDecodedAudioFilePath = null;
    }

    private static string GetAudioFileExtension(byte[] audioBytes)
    {
        if (audioBytes.Length >= 4)
        {
            if (audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F')
            {
                return ".wav";
            }

            if (audioBytes[0] == 'O' && audioBytes[1] == 'g' && audioBytes[2] == 'g' && audioBytes[3] == 'S')
            {
                return ".ogg";
            }

            if (audioBytes[0] == 'f' && audioBytes[1] == 'L' && audioBytes[2] == 'a' && audioBytes[3] == 'C')
            {
                return ".flac";
            }
        }

        if (audioBytes.Length >= 3 && audioBytes[0] == 'I' && audioBytes[1] == 'D' && audioBytes[2] == '3')
        {
            return ".mp3";
        }

        return ".tmp";
    }
}
