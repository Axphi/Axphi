using Axphi.Data;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Axphi.Services
{
    // ================= 🌟 终极音游 0 延迟混音引擎 =================
    public static class HitSoundManager
    {
        private static WaveOutEvent? _outputDevice;
        private static MixingSampleProvider? _mixer;
        private static Dictionary<NoteKind, CachedSound> _soundCache = new();
        private static bool _isInitialized = false;

        public static void Init()
        {
            if (_isInitialized) return;

            try
            {
                // 1. 创建全局混音器 (强行锁定 44100Hz 双声道)
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                {
                    ReadFully = true // 核心魔法：保持静音播放，随时准备接客
                };

                // 2. 绑定到极低延迟的 WaveOutEvent
                _outputDevice = new WaveOutEvent { DesiredLatency = 50 };
                _outputDevice.Init(_mixer);
                _outputDevice.Play();

                // 3. 把硬盘里的音效一次性吸入内存！
                LoadSoundIntoCache(NoteKind.Tap, @"Resources\Sounds\tap.wav");
                LoadSoundIntoCache(NoteKind.Drag, @"Resources\Sounds\drag.wav");
                LoadSoundIntoCache(NoteKind.Flick, @"Resources\Sounds\flick.wav");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"混音器初始化爆炸: {ex.Message}");
            }
        }

        private static void LoadSoundIntoCache(NoteKind kind, string relativePath)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return;

            try
            {
                using var audioFile = new AudioFileReader(fullPath);
                ISampleProvider provider = audioFile;

                // 强行把所有单声道音效转成双声道
                if (provider.WaveFormat.Channels == 1)
                    provider = new MonoToStereoSampleProvider(provider);

                // 强行统一采样率为 44100
                if (provider.WaveFormat.SampleRate != 44100)
                    provider = new WdlResamplingSampleProvider(provider, 44100);

                // 读取成 float 数组死死锁在内存里
                var wholeFile = new List<float>();
                var readBuffer = new float[44100 * 2];
                int samplesRead;
                while ((samplesRead = provider.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }

                _soundCache[kind] = new CachedSound(wholeFile.ToArray(), _mixer!.WaveFormat);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取音效 {relativePath} 失败: {ex.Message}");
            }
        }

        public static void Play(NoteKind kind)
        {
            if (!_soundCache.ContainsKey(kind)) kind = NoteKind.Tap;

            if (!_isInitialized || !_soundCache.TryGetValue(kind, out var cachedSound)) return;

            // 从内存直接丢一个指针给混音器
            _mixer!.AddMixerInput(new CachedSoundSampleProvider(cachedSound));
        }
    }

    // --- 内存音频载体 ---
    public class CachedSound
    {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }
        public CachedSound(float[] audioData, WaveFormat waveFormat)
        {
            AudioData = audioData;
            WaveFormat = waveFormat;
        }
    }

    // --- 内存音频播放指针 ---
    public class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cachedSound;
        private long _position;
        public CachedSoundSampleProvider(CachedSound cachedSound) => _cachedSound = cachedSound;
        public WaveFormat WaveFormat => _cachedSound.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = _cachedSound.AudioData.Length - _position;
            var samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(_cachedSound.AudioData, _position, buffer, offset, (int)samplesToCopy);
            _position += samplesToCopy;
            return (int)samplesToCopy;
        }
    }
}