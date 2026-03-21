using Axphi.Data;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Threading.Tasks;


namespace Axphi.Services
{
    // ================= 🌟 终极音游 0 延迟混音引擎 =================
    public static class HitSoundManager
    {
        private static WasapiOut? _outputDevice;
        private static MixingSampleProvider? _mixer;
        private static Dictionary<NoteKind, CachedSound> _soundCache = new();
        private static bool _isInitialized = false;

        // ================= 🌟 新增：设备监听器 =================
        private static MMDeviceEnumerator? _deviceEnumerator;
        private static AudioDeviceNotificationClient? _notificationClient;

        public static void Init()
        {
            if (_isInitialized) return;

            try
            {
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                {
                    ReadFully = true
                };

                _outputDevice = new WasapiOut(AudioClientShareMode.Shared, 50);
                _outputDevice.Init(_mixer);
                _outputDevice.Play();

                // ================= 🌟 核心魔法：注册热插拔监听 =================
                _deviceEnumerator = new MMDeviceEnumerator();
                _notificationClient = new AudioDeviceNotificationClient();
                // 订阅设备改变事件
                _notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
                _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);
                // ===============================================================

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

        // 🌟 当系统检测到你插拔了耳机，就会触发这个方法
        private static void OnDefaultDeviceChanged()
        {
            // 必须扔到后台线程去执行，防止和 Windows 的底层音频回调产生死锁
            Task.Run(() =>
            {
                try
                {
                    // 给 Windows 切换声卡驱动留一点点缓冲时间 (500毫秒)
                    System.Threading.Thread.Sleep(500);

                    // 1. 掐死旧的、失效的输出设备
                    if (_outputDevice != null)
                    {
                        _outputDevice.Stop();
                        _outputDevice.Dispose();
                    }

                    // 2. 重新初始化一个！它会自动绑定到最新的默认设备（耳机/外放）
                    _outputDevice = new WasapiOut(AudioClientShareMode.Shared, 50);
                    _outputDevice.Init(_mixer!);
                    _outputDevice.Play();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设备热插拔自动重启失败: {ex.Message}");
                }
            });
        }

        private static void LoadSoundIntoCache(NoteKind kind, string relativePath)
        {
            // ... (这里保留你原本的 LoadSoundIntoCache 代码，带 volumeFactor 压缩的那个版本)
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

                // 防爆音系数
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
            // ... (这里保留你原本的 Play 代码)
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




    // --- 监听 Windows 默认音频设备改变 ---
    public class AudioDeviceNotificationClient : IMMNotificationClient
    {
        public event Action? DefaultDeviceChanged;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            // 当系统默认的输出设备（Render + Console）发生改变时触发
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
}