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
    public partial class AudioTrackViewModel : ObservableObject, ISelectionNode, ITimelineDraggable, ILayerPointerInteractable
    {
        public TimelineViewModel _timeline;
        public TimelineViewModel Timeline => _timeline;
        private readonly IMessenger _messenger;

        private readonly ProjectManager _projectManager;
        public Chart Chart { get; }

        [ObservableProperty]
        private bool _isExpanded = false;

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
                double startSeconds = TimeTickConverter.TickToTime(AudioOffsetTicks, Chart.BpmKeyFrames, Chart.InitialBpm);
                double endSeconds = startSeconds + AudioDurationSeconds;
                double exactEndTick = TimeTickConverter.TimeToTick(endSeconds, Chart.BpmKeyFrames, Chart.InitialBpm);
                return (int)Math.Round(exactEndTick - AudioOffsetTicks, MidpointRounding.AwayFromZero);
            }
        }

        [ObservableProperty]
        private double _layerPixelXOffset;

        [ObservableProperty]
        private double _layerPixelWidth;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLayerHighlighted))]
        private bool _isLayerSelected;

        bool ISelectionNode.IsSelected
        {
            get => IsLayerSelected;
            set => IsLayerSelected = value;
        }

        [ObservableProperty]
        private bool _isDragLocked;

        public bool IsLayerHighlighted => IsLayerSelected;

        private double _layerVirtualPixelX;
        private int _lastAppliedTick;
        private bool _wasSelectedBeforeLayerGesture;
        private double _layerGestureDistance;

        private ProjectMetadata GetMetadata()
        {
            _projectManager.EditingProject ??= new Project { Chart = Chart };
            _projectManager.EditingProject.Metadata ??= new ProjectMetadata();
            return _projectManager.EditingProject.Metadata;
        }

        private int AudioOffsetTicks
        {
            get => GetMetadata().AudioOffsetTicks;
            set => GetMetadata().AudioOffsetTicks = value;
        }

        partial void OnIsExpandedChanged(bool value) => GetMetadata().IsAudioTrackExpanded = value;

        partial void OnIsDragLockedChanged(bool value)
        {
            GetMetadata().IsAudioTrackLocked = value;

            // 锁定后音频图层不可被选中，若当前已选中则立即取消。
            if (value && IsLayerSelected)
            {
                IsLayerSelected = false;
            }
        }


        public AudioTrackViewModel(Chart chart, TimelineViewModel timeline, ProjectManager projectManager, IMessenger messenger)
        {
            Chart = chart;
            _timeline = timeline;
            _projectManager = projectManager;
            _messenger = messenger;

            IsExpanded = GetMetadata().IsAudioTrackExpanded;
            IsDragLocked = GetMetadata().IsAudioTrackLocked;

            UpdatePixels();

            _messenger.Register<AudioTrackViewModel, ZoomScaleChangedMessage>(this, (r, m) => r.UpdatePixels());
            _messenger.Register<AudioTrackViewModel, KeyframesNeedSortMessage>(this, (r, m) => r.UpdatePixels());
            _messenger.Register<AudioTrackViewModel, ClearSelectionMessage>(this, (r, m) =>
            {
                if (m.Group == SelectionGroup.Layers && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.IsLayerSelected = false;
                }
            });
            _messenger.Register<AudioTrackViewModel, LayersDragStartedMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragStarted();
                }
            });
            _messenger.Register<AudioTrackViewModel, LayersDragDeltaMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragDelta(m.HorizontalChange, m.DeltaTick);
                }
            });
            _messenger.Register<AudioTrackViewModel, LayersDragCompletedMessage>(this, (r, m) =>
            {
                if (r.IsLayerSelected && !ReferenceEquals(r, m.SenderToIgnore))
                {
                    r.ReceiveLayerDragCompleted();
                }
            });

            // 订阅音频导入事件
            _messenger.Register<AudioTrackViewModel, AudioLoadedMessage>(this, (r, m) =>
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
            LayerPixelXOffset = _timeline.TickToPixel(AudioOffsetTicks);
            LayerPixelWidth = Math.Max(10, _timeline.TickToPixel(AudioDurationTicks));
        }

        public void HandleLayerPointerDown()
        {
            if (IsDragLocked)
            {
                return;
            }

            _timeline.EnterLayerSelectionContext(this);
            _layerGestureDistance = 0;
            _wasSelectedBeforeLayerGesture = SelectionHelper.BeginSelectionGesture("Layers", this, IsLayerSelected, value => IsLayerSelected = value);
        }

        public void HandleLayerPointerUp()
        {
            if (IsDragLocked)
            {
                return;
            }

            SelectionHelper.CompleteSelectionGesture("Layers", this, _wasSelectedBeforeLayerGesture, _layerGestureDistance, value => IsLayerSelected = value);
        }

        public void OnLayerDragStarted()
        {
            if (IsDragLocked)
            {
                return;
            }

            if (!IsLayerSelected)
            {
                HandleLayerPointerDown();
            }

            ReceiveLayerDragStarted();

            if (IsLayerSelected)
            {
                _messenger.Send(new LayersDragStartedMessage(this));
            }
        }

        public void OnLayerDragDelta(double horizontalChange)
        {
            if (IsDragLocked)
            {
                return;
            }

            double nextVirtualPixelX = _layerVirtualPixelX + horizontalChange;

            double exactTick = _timeline.PixelToTick(nextVirtualPixelX);
            int snappedTick = _timeline.SnapToClosest(exactTick, isPlayhead: false);
            int deltaTick = snappedTick - _lastAppliedTick;

            if (IsLayerSelected)
            {
                _messenger.Send(new LayersDragDeltaMessage(horizontalChange, deltaTick, this));
            }

            ReceiveLayerDragDelta(horizontalChange, deltaTick);
        }

        public void OnLayerDragCompleted()
        {
            if (IsDragLocked)
            {
                return;
            }

            if (IsLayerSelected)
            {
                _messenger.Send(new LayersDragCompletedMessage(this));
            }

            ReceiveLayerDragCompleted();
        }

        public void OnDragStarted() => OnLayerDragStarted();

        public void OnDragDelta(double horizontalChange) => OnLayerDragDelta(horizontalChange);

        public void OnDragCompleted() => OnLayerDragCompleted();

        private void ReceiveLayerDragStarted()
        {
            _layerVirtualPixelX = LayerPixelXOffset;
            _lastAppliedTick = AudioOffsetTicks;
        }

        private void ReceiveLayerDragDelta(double horizontalChange, int deltaTick)
        {
            _layerGestureDistance += Math.Abs(horizontalChange);
            _layerVirtualPixelX += horizontalChange;

            if (deltaTick != 0)
            {
                _lastAppliedTick += deltaTick;
                AudioOffsetTicks = _lastAppliedTick;
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
            AudioOffsetTicks = _lastAppliedTick;
            UpdatePixels();
        }

        public void DeleteAudio()
        {
            if (_projectManager.EditingProject != null)
            {
                _projectManager.EditingProject.EncodedAudio = null;
            }

            AudioOffsetTicks = 0;
            AudioDurationSeconds = 0;
            WaveformPeaks = null;
            IsLayerSelected = false;
            IsDragLocked = false;
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
            get => GetMetadata().AudioVolume;
            set
            {
                if (GetMetadata().AudioVolume != value)
                {
                    GetMetadata().AudioVolume = value;
                    OnPropertyChanged(); // 通知 UI 音量变了！
                    WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
                }
            }
        }
    }
}