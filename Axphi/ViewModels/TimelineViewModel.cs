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