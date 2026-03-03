using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Axphi.Data; // 替换成你 Chart 和 JudgementLine 所在的实际命名空间
using System.Collections.ObjectModel;

namespace Axphi.ViewModels
{
    // 必须继承 ObservableObject 才能使用 MVVM 魔法
    public partial class TimelineViewModel : ObservableObject
    {
        // 核心数据：需要暴露给界面的谱面对象
        [ObservableProperty]
        private Chart _currentChart;

        // 1. 缩放比例 (Zoom)：相当于按住 Alt 滚轮修改的值，默认是 1.0
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPixelWidth))] // 当 Zoom 改变时，通知界面重新获取总宽度
        private double _zoomScale = 1.0;

        // 2. 谱面总长度 (以 128分音符/Tick 为单位)
        // 假设一首 2 分钟的 120BPM 歌曲，大约有 240 拍 * 32 = 7680 个 Tick。我们先给个默认值 10000 够长了。
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPixelWidth))]
        private int _totalDurationTicks = 10000;

        // 3. 基础缩放系数：1 个 Tick 默认占多少像素？
        // 如果给 1.0，那 10000 个 Tick 就是 10000 像素，太长了。我们默认给个 0.5 像素试试。
        private const double BasePixelsPerTick = 0.5;

        // 4. 【核心魔法】计算出右侧轨道的物理总像素宽度！UI 会绑定这个值！
        public double TotalPixelWidth => TotalDurationTicks * BasePixelsPerTick * ZoomScale;

        // === 游标核心属性 ===

        // 接收来自 ChartDisplay 的实时播放秒数
        [ObservableProperty]
        private double _currentPlayTimeSeconds;

        // 供 XAML 里红色游标绑定的 X 物理坐标
        [ObservableProperty]
        private double _playheadPositionX;

        // 1. 当播放器的时间改变时，重新计算游标位置
        partial void OnCurrentPlayTimeSecondsChanged(double value)
        {
            UpdatePlayheadPosition();
        }

        // 2. 当你按 Alt+滚轮 缩放时，游标位置也必须跟着伸缩！
        partial void OnZoomScaleChanged(double value)
        {
            // 注意：因为 TotalPixelWidth 用了 NotifyPropertyChangedFor
            // 它会自动更新，但我们必须手动调用更新游标
            UpdatePlayheadPosition();
        }

        // 核心换算公式（完全复刻你 ChartRenderer 里的算法）
        private void UpdatePlayheadPosition()
        {
            if (CurrentChart == null) return;

            // 获取当前 BPM (暂时取第一个)
            double currentBpm = 120.0;
            if (CurrentChart.BpmKeyFrames != null && CurrentChart.BpmKeyFrames.Any())
            {
                currentBpm = CurrentChart.BpmKeyFrames.First().Value;
            }

            // 秒数 转 Tick (记得加上谱面的 Offset)
            double secondsPerTick = 1.875 / currentBpm;

            

            // 修改后：强行把小数抹掉，让它永远精确咬合在整数的 Tick 刻度上！
            int currentTick = (int)((CurrentPlayTimeSeconds / secondsPerTick) + CurrentChart.Offset);

            // Tick 转 物理像素
            PlayheadPositionX = currentTick * BasePixelsPerTick * ZoomScale;
        }

        // ================= 新增：专供 UI 绑定的轨道视图模型集合 =================
        public ObservableCollection<TrackViewModel> Tracks { get; } = new ObservableCollection<TrackViewModel>();

        // 构造函数：初始化时，可以先给个空谱面，或者由外部传进来
        public TimelineViewModel()
        {
            // 确保集合不会是 null，防止界面绑定报错
            CurrentChart = new Chart();
            if (CurrentChart.JudgementLines == null)
            {
                CurrentChart.JudgementLines = new ObservableCollection<JudgementLine>();
            }
        }

        // 核心命令：点击“+添加判定线”时触发
        [RelayCommand]
        private void AddJudgementLine()
        {
            if (CurrentChart == null || CurrentChart.JudgementLines == null) return;

            // 1. 实例化底层的纯净数据
            // 新建一条判定线
            var newLine = new JudgementLine();

            // 把新线加进集合，界面会自动更新！
            CurrentChart.JudgementLines.Add(newLine);

            // 2. 实例化这个数据的“代理人”，并加进 UI 集合里！
            var newTrackVM = new TrackViewModel(newLine, $"判定线图层 {Tracks.Count + 1}");
            Tracks.Add(newTrackVM);
        }
    }
}