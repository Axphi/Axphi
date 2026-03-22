using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Axphi.ViewModels
{
    public partial class AudioTrackViewModel : ObservableObject
    {
        public TimelineViewModel _timeline;
        public TimelineViewModel Timeline => _timeline;

        private readonly ProjectManager _projectManager;
        public Chart Chart { get; }

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AudioDurationTicks))]
        private double _audioDurationSeconds = 0;

        // ================= 🌟 新增：存放波形峰值数据的数组 =================
        [ObservableProperty]
        private float[]? _waveformPeaks;

        // ================= 基于积分的动态跨度计算 =================
        public int AudioDurationTicks
        {
            get
            {
                if (Chart == null || AudioDurationSeconds <= 0) return 0;
                double startSeconds = TimeTickConverter.TickToTime(Chart.Offset, Chart.BpmKeyFrames, Chart.InitialBpm);
                double endSeconds = startSeconds + AudioDurationSeconds;
                double exactEndTick = TimeTickConverter.TimeToTick(endSeconds, Chart.BpmKeyFrames, Chart.InitialBpm);
                return (int)Math.Round(exactEndTick - Chart.Offset, MidpointRounding.AwayFromZero);
            }
        }

        [ObservableProperty]
        private double _layerPixelXOffset;

        [ObservableProperty]
        private double _layerPixelWidth;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLayerHighlighted))]
        private bool _isLayerSelected;

        public bool IsLayerHighlighted => IsLayerSelected;

        private double _layerVirtualPixelX;
        private int _lastAppliedTick;


        public AudioTrackViewModel(Chart chart, TimelineViewModel timeline, ProjectManager projectManager)
        {
            Chart = chart;
            _timeline = timeline;
            _projectManager = projectManager;

            UpdatePixels();

            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, ZoomScaleChangedMessage>(this, (r, m) => r.UpdatePixels());
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, KeyframesNeedSortMessage>(this, (r, m) => r.UpdatePixels());
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, ClearSelectionMessage>(this, (r, m) =>
            {
                if (m.GroupName == "Layers" && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.IsLayerSelected = false;
                }
            });
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, LayersDragStartedMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragStarted();
                }
            });
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, LayersDragDeltaMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragDelta(m.HorizontalChange, m.DeltaTick);
                }
            });
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, LayersDragCompletedMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragCompleted();
                }
            });

            // 订阅音频导入事件
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, AudioLoadedMessage>(this, (r, m) =>
            {
                // 改为异步调用，防止扫描波形时卡住界面
                _ = r.LoadAudioDataFromFileAsync(m.FilePath);
            });

            // 防御性加载
            if (_projectManager.EditingProject?.EncodedAudio != null)
            {
                _ = LoadAudioDataFromBytesAsync(_projectManager.EditingProject.EncodedAudio);
            }
        }

        public void UpdatePixels()
        {
            LayerPixelXOffset = _timeline.TickToPixel(Chart.Offset);
            LayerPixelWidth = Math.Max(10, _timeline.TickToPixel(AudioDurationTicks));
        }

        public void HandleLayerPointerDown()
        {
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", null));
            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", null));

            bool isShiftDown = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift);
            bool isCtrlDown = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control);

            if (IsLayerSelected)
            {
                if (isCtrlDown)
                {
                    IsLayerSelected = false;
                }
                else if (isShiftDown)
                {
                    IsLayerSelected = true;
                }

                return;
            }

            SelectionHelper.HandleSelection("Layers", this, IsLayerSelected, value => IsLayerSelected = value);
        }

        public void OnLayerDragStarted()
        {
            if (!IsLayerSelected)
            {
                HandleLayerPointerDown();
            }

            ReceiveLayerDragStarted();

            if (IsLayerSelected)
            {
                WeakReferenceMessenger.Default.Send(new LayersDragStartedMessage(this));
            }
        }

        public void OnLayerDragDelta(double horizontalChange)
        {
            double nextVirtualPixelX = _layerVirtualPixelX + horizontalChange;

            double exactTick = _timeline.PixelToTick(nextVirtualPixelX);
            int snappedTick = _timeline.SnapToClosest(exactTick, isPlayhead: false);
            int deltaTick = snappedTick - _lastAppliedTick;

            if (IsLayerSelected)
            {
                WeakReferenceMessenger.Default.Send(new LayersDragDeltaMessage(horizontalChange, deltaTick, this));
            }

            ReceiveLayerDragDelta(horizontalChange, deltaTick);
        }

        public void OnLayerDragCompleted()
        {
            if (IsLayerSelected)
            {
                WeakReferenceMessenger.Default.Send(new LayersDragCompletedMessage(this));
            }

            ReceiveLayerDragCompleted();
        }

        private void ReceiveLayerDragStarted()
        {
            _layerVirtualPixelX = LayerPixelXOffset;
            _lastAppliedTick = Chart.Offset;
        }

        private void ReceiveLayerDragDelta(double horizontalChange, int deltaTick)
        {
            _layerVirtualPixelX += horizontalChange;

            if (deltaTick != 0)
            {
                _lastAppliedTick += deltaTick;
                Chart.Offset = _lastAppliedTick;
                LayerPixelWidth = Math.Max(10, _timeline.TickToPixel(AudioDurationTicks));
            }

            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            {
                LayerPixelXOffset = _timeline.TickToPixel(_lastAppliedTick);
            }
            else
            {
                LayerPixelXOffset = _layerVirtualPixelX;
            }
        }

        private void ReceiveLayerDragCompleted()
        {
            Chart.Offset = _lastAppliedTick;
            UpdatePixels();
        }

        public void DeleteAudio()
        {
            if (_projectManager.EditingProject != null)
            {
                _projectManager.EditingProject.EncodedAudio = null;
            }

            Chart.Offset = 0;
            AudioDurationSeconds = 0;
            WaveformPeaks = null;
            IsLayerSelected = false;
            UpdatePixels();
        }

        // ================= 🌟 异步解析音频时长与波形 =================
        private async Task LoadAudioDataFromFileAsync(string filePath)
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                AudioDurationSeconds = reader.TotalTime.TotalSeconds;
                UpdatePixels();

                // 提取波形数据（交给后台线程算，算完更新 UI）
                WaveformPeaks = await Task.Run(() => GetPeaks(reader));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取音频时长失败: {ex.Message}");
            }
        }

        private async Task LoadAudioDataFromBytesAsync(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length == 0) return;
            try
            {
                string tempFile = Path.GetTempFileName();
                File.WriteAllBytes(tempFile, audioBytes);

                using var reader = new MediaFoundationReader(tempFile);
                AudioDurationSeconds = reader.TotalTime.TotalSeconds;
                UpdatePixels();

                // MediaFoundationReader 默认吐出的是 byte[]，我们需要转换成 Float 采样提供器
                var sampleProvider = reader.ToSampleProvider();
                WaveformPeaks = await Task.Run(() => GetPeaks(sampleProvider));

                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从内存读取音频时长失败: {ex.Message}");
            }
        }

        // ================= 🌟 核心算法：提取音频包络峰值 =================
        private float[] GetPeaks(ISampleProvider provider)
        {
            // 我们设定一秒钟提取 100 个点（10ms 一个点，这个精度对于画图足够了且性能好）
            int samplesPerPixel = provider.WaveFormat.SampleRate * provider.WaveFormat.Channels / 100;
            float[] buffer = new float[samplesPerPixel];
            int read;
            var peakList = new List<float>();

            // 一段一段读，找出这段里面声音最大的那个点（振幅）
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                float max = 0;
                for (int i = 0; i < read; i++)
                {
                    float val = Math.Abs(buffer[i]);
                    if (val > max) max = val;
                }
                peakList.Add(max);
            }
            return peakList.ToArray();
        }


        

        // ================= 🌟 新增：供前端绑定的音量属性 =================
        public double AudioVolume
        {
            get => Chart.AudioVolume;
            set
            {
                if (Chart.AudioVolume != value)
                {
                    Chart.AudioVolume = value;
                    OnPropertyChanged(); // 通知 UI 音量变了！
                }
            }
        }
    }
}