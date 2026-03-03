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