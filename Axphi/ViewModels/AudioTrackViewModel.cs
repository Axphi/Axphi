using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NAudio.Wave;
using System;
using System.IO;

namespace Axphi.ViewModels
{
    public partial class AudioTrackViewModel : ObservableObject
    {
        public TimelineViewModel _timeline;
        // 🌟 暴露给 XAML 和 UI 代码后台使用
        public TimelineViewModel Timeline => _timeline;

        private readonly ProjectManager _projectManager;

        public Chart Chart { get; }

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AudioDurationTicks))]
        private double _audioDurationSeconds = 0;

        // ================= 🌟 核心修复：基于积分的动态跨度计算 =================
        public int AudioDurationTicks
        {
            get
            {
                if (Chart == null || AudioDurationSeconds <= 0) return 0;

                // 1. 算出音频图层左边界（Offset）在宇宙中的绝对起步秒数
                double startSeconds = TimeTickConverter.TickToTime(Chart.Offset, Chart.BpmKeyFrames, Chart.InitialBpm);

                // 2. 加上音频自身的物理总时长（秒），得到它结束时的绝对秒数
                double endSeconds = startSeconds + AudioDurationSeconds;

                // 3. 召唤你的积分器！把结束的物理秒数，反推回宇宙中绝对的结束 Tick！
                double exactEndTick = TimeTickConverter.TimeToTick(endSeconds, Chart.BpmKeyFrames, Chart.InitialBpm);

                // 4. 结束的 Tick 减去 起步的 Tick(Offset) = 这个音频在当前 BPM 环境下跨越的总 Tick 长度！
                return (int)Math.Round(exactEndTick - Chart.Offset, MidpointRounding.AwayFromZero);
            }
        }

        // ================= 🌟 改为和 TrackViewModel 相同的独立 UI 属性 =================
        [ObservableProperty]
        private double _layerPixelXOffset;

        [ObservableProperty]
        private double _layerPixelWidth;


        public AudioTrackViewModel(Chart chart, TimelineViewModel timeline, ProjectManager projectManager)
        {
            Chart = chart;
            _timeline = timeline;
            _projectManager = projectManager;

            // 1. 出生时计算一次
            UpdatePixels();

            // 2. 完美适配 Alt 缩放
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, ZoomScaleChangedMessage>(this, (r, m) =>
            {
                r.UpdatePixels();
            });

            // 3. 订阅音频导入事件
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, AudioLoadedMessage>(this, (r, m) =>
            {
                r.LoadDurationFromFile(m.FilePath);
            });

            // 4. 防御性加载
            if (_projectManager.EditingProject?.EncodedAudio != null)
            {
                LoadDurationFromBytes(_projectManager.EditingProject.EncodedAudio);
            }


            // (加在构造函数里) 监听关键帧变动（包括 BPM 关键帧被拖拽重排）
            WeakReferenceMessenger.Default.Register<AudioTrackViewModel, KeyframesNeedSortMessage>(this, (r, m) =>
            {
                r.UpdatePixels();
            });
        }

        // 根据底层数据（Chart.Offset 和 Duration）强制重算像素！
        public void UpdatePixels()
        {
            LayerPixelXOffset = _timeline.TickToPixel(Chart.Offset);
            LayerPixelWidth = Math.Max(10, _timeline.TickToPixel(AudioDurationTicks));
            LayerPixelWidth = Math.Max(10, _timeline.TickToPixel(AudioDurationTicks));
        }

        private void LoadDurationFromFile(string filePath)
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                AudioDurationSeconds = reader.TotalTime.TotalSeconds;
                UpdatePixels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取音频时长失败: {ex.Message}");
            }
        }

        private void LoadDurationFromBytes(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length == 0) return;
            try
            {
                string tempFile = Path.GetTempFileName();
                File.WriteAllBytes(tempFile, audioBytes);

                using (var reader = new MediaFoundationReader(tempFile))
                {
                    AudioDurationSeconds = reader.TotalTime.TotalSeconds;
                }

                File.Delete(tempFile);
                UpdatePixels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从内存读取音频时长失败: {ex.Message}");
            }
        }
    }
}