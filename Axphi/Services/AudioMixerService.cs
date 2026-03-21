using Axphi.Data;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Axphi.Services
{
    // ================= 🌟 1. 独立出来的系统音频硬件监听服务 =================
    public static class SystemAudioMonitor
    {
        private static MMDeviceEnumerator? _deviceEnumerator;
        private static AudioDeviceNotificationClient? _notificationClient;
        private static bool _isInitialized;

        // 统一的全局事件：任何需要“热插拔重启”的播放器都可以订阅它！
        public static event Action? OnDefaultDeviceChanged;

        public static void Init()
        {
            if (_isInitialized) return;

            _deviceEnumerator = new MMDeviceEnumerator();
            _notificationClient = new AudioDeviceNotificationClient();

            _notificationClient.DefaultDeviceChanged += () =>
            {
                // 统一在这里做后台线程等待，避免死锁，并且不需要每个播放器都自己写 Task.Run
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(500); // 给 Windows 驱动留出缓冲时间
                    OnDefaultDeviceChanged?.Invoke();
                });
            };

            _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);
            _isInitialized = true;
        }

        public static void Cleanup()
        {
            if (_deviceEnumerator != null && _notificationClient != null)
            {
                _deviceEnumerator.UnregisterEndpointNotificationCallback(_notificationClient);
                _deviceEnumerator.Dispose();
                _deviceEnumerator = null;
                _notificationClient = null;
            }
        }
    }

    // --- NAudio 底层接口实现 (被隐藏在服务中) ---
    public class AudioDeviceNotificationClient : IMMNotificationClient
    {
        public event Action? DefaultDeviceChanged;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Console)
            {
                DefaultDeviceChanged?.Invoke();
            }
        }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string pwstrDeviceId) { }
        public void OnDeviceStateChanged(string pwstrDeviceId, DeviceState dwNewState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }


    // ================= 🌟 2. 终极音游 0 延迟混音引擎 =================
    public static class HitSoundManager
    {
        private static WasapiOut? _outputDevice;
        private static MixingSampleProvider? _mixer;
        private static Dictionary<NoteKind, CachedSound> _soundCache = new();
        private static bool _isInitialized = false;

        public static void Init()
        {
            if (_isInitialized) return;

            try
            {
                // 1. 启动全局硬件监听服务
                SystemAudioMonitor.Init();
                // 2. 订阅热插拔事件
                SystemAudioMonitor.OnDefaultDeviceChanged += RestartDevice;

                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                {
                    ReadFully = true
                };

                _outputDevice = new WasapiOut(AudioClientShareMode.Shared, 50);
                _outputDevice.Init(_mixer);
                _outputDevice.Play();

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

        private static void RestartDevice()
        {
            try
            {
                if (_outputDevice != null)
                {
                    _outputDevice.Stop();
                    _outputDevice.Dispose();
                }

                _outputDevice = new WasapiOut(AudioClientShareMode.Shared, 50);
                _outputDevice.Init(_mixer!);
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HitSound 热插拔重启失败: {ex.Message}");
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

                if (provider.WaveFormat.Channels == 1)
                    provider = new MonoToStereoSampleProvider(provider);

                if (provider.WaveFormat.SampleRate != 44100)
                    provider = new WdlResamplingSampleProvider(provider, 44100);

                var wholeFile = new List<float>();
                var readBuffer = new float[44100 * 2];
                int samplesRead;

                float volumeFactor = 0.5f;

                while ((samplesRead = provider.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    for (int i = 0; i < samplesRead; i++) readBuffer[i] *= volumeFactor;
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