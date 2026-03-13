using Axphi.Data; // 替换成你 Chart 和 JudgementLine 所在的实际命名空间
using Axphi.Data.KeyFrames;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;

namespace Axphi.ViewModels
{
    // 必须继承 ObservableObject 才能使用 MVVM 魔法
    public partial class TimelineViewModel : ObservableObject
    {

        // 【新增】保存全局数据源的引用
        private readonly ProjectManager _projectManager;

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

            //  【新增】算出当前的 Tick
            int currentTick = GetCurrentTick();

            //  【新增】拿到当前谱面的缓动方向设置
            var easingDirection = CurrentChart?.KeyFrameEasingDirection ?? default;

            // ================= ✨ 塞入第二步：让 BPM 跟着播放器跑！ =================
            BpmTrack?.SyncValuesToTime(currentTick);
            // =======================================================================


            //  【新增】大点兵！让所有的轨道根据当前时间更新自己的数值面板！
            foreach (var track in Tracks)
            {
                track.SyncValuesToTime(currentTick, easingDirection);
            }
        }

        // 2. 当你按 Alt+滚轮 缩放时，游标位置也必须跟着伸缩！
        partial void OnZoomScaleChanged(double value)
        {
            // 注意：因为 TotalPixelWidth 用了 NotifyPropertyChangedFor
            // 它会自动更新，但我们必须手动调用更新游标
            UpdatePlayheadPosition();
            // 告诉全网：缩放变了！所有的关键帧请重新计算你们的 X 坐标！
            WeakReferenceMessenger.Default.Send(new ZoomScaleChangedMessage(value));
        }

        // 核心换算公式
        private void UpdatePlayheadPosition()
        {
            if (CurrentChart == null) return;

            // 1. 拿到积分器算出来的、绝对准确的当前 Tick！(取代了旧的乘除法)
            double currentTick = GetExactTick();

            // 2. 把 Tick 转换成屏幕上的像素 X 坐标！
            PlayheadPositionX = TickToPixel(currentTick);
        }


        // ✨ 全局唯一的 BPM 轨道！
        [ObservableProperty]
        private BpmTrackViewModel? _bpmTrack;

        // ================= 新增：专供 UI 绑定的轨道视图模型集合 =================
        public ObservableCollection<TrackViewModel> Tracks { get; } = new ObservableCollection<TrackViewModel>();

        // 构造函数：初始化时，可以先给个空谱面，或者由外部传进来
        public TimelineViewModel(ProjectManager projectManager)
        {

            _projectManager = projectManager; // 存进私有变量

            // 然后再从 manager 里把 Chart 拿出来赋值给 _currentChart
            // 极其重要的防坑提示：软件刚启动时，工程可能是空的！所以要做个判空！
            if (_projectManager.EditingProject != null)
            {
                ReloadTracksFromCurrentChart();
            }
            else
            {
                // 如果刚启动时还没加载工程，给个空的占位，防止界面绑定报错
                CurrentChart = new Chart();

                
            }


            WeakReferenceMessenger.Default.Register<TimelineViewModel, ProjectLoadedMessage>(this, (recipient, message) =>
            {
                // 重新去抱 ProjectManager 的大腿！拿到最新的“谱面B”！
                if (recipient._projectManager.EditingProject != null)
                {
                    recipient.CurrentChart = recipient._projectManager.EditingProject.Chart;
                    recipient.Tracks.Clear();
                    // 收到换工程的广播后，立刻执行重建动作！
                    recipient.ReloadTracksFromCurrentChart();
                }
            });


        }

        // 核心命令：点击“+添加判定线”时触发
        [RelayCommand]
        private void AddJudgementLine()
        {
            

            // 1. 实例化底层的纯净数据
            // 新建一条判定线
            var newLine = new JudgementLine();

            // 把新线加进集合，界面会自动更新！
            CurrentChart.JudgementLines.Add(newLine);
            // 发出消息
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            // 2. 实例化这个数据的“代理人”，并加进 UI 集合里！
            var newTrackVM = new TrackViewModel(newLine, $"判定线图层 {Tracks.Count + 1}",this);
            Tracks.Add(newTrackVM);
        }

        // ================= 【新增的核心函数】 =================
        /// <summary>
        /// 根据当前 ProjectManager 里的真实谱面，重新生成左侧的所有 Track UI
        /// </summary>
        private void ReloadTracksFromCurrentChart()
        {
            if (_projectManager.EditingProject == null || _projectManager.EditingProject.Chart == null)
                return;

            // 1. 换绑剧本：把指针指向最新的真实谱面
            CurrentChart = _projectManager.EditingProject.Chart;

            // ================= ✨ 塞入第一步：实例化 BPM 轨道！ =================
            BpmTrack = new BpmTrackViewModel(CurrentChart, this);
            // =====================================================================

            // 2. 砸碎旧舞台：清空前端的 Track UI 集合 (这一步让旧 UI 被 GC 回收)
            Tracks.Clear();

            // 3. 请上新演员：遍历新谱面里的判定线，挨个给它们创建前端代理人
            if (CurrentChart.JudgementLines != null)
            {
                for (int i = 0; i < CurrentChart.JudgementLines.Count; i++)
                {
                    var line = CurrentChart.JudgementLines[i];
                    // 名字自动按序号排：判定线图层 1, 判定线图层 2...
                    var newTrackVM = new TrackViewModel(line, $"判定线图层 {i + 1}",this);
                    Tracks.Add(newTrackVM);
                }
            }

            // 4. (可选) 让时间轴游标归零
            CurrentPlayTimeSeconds = 0;

            // 5. 顺便大喊一声，让右侧的渲染器也强制刷新一下画面！
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }


        // 在 TimelineViewModel.cs 里加上这个方法 (上一回合提过，确认一下你加上了)
        public int GetCurrentTick()
        {
            if (CurrentChart == null) return 0;

            // ✨ 核心修复：直接用积分器算 Tick，绝对不会突变！
            double exactTick = TimeTickConverter.TimeToTick(CurrentPlayTimeSeconds, CurrentChart.BpmKeyFrames, 120.0);
            return (int)exactTick + CurrentChart.Offset;
        }

        // 在 TimelineViewModel 里加上这个公开的换算方法
        public double TickToPixel(double tick)
        {
            return tick * BasePixelsPerTick * ZoomScale;
        }
        // 1. 新增一个获取精确小数 Tick 的方法
        public double GetExactTick()
        {
            if (CurrentChart == null) return 0;
            // 不做强制 int 转换，原汁原味返回精确小数
            double exactTick = TimeTickConverter.TimeToTick(CurrentPlayTimeSeconds, CurrentChart.BpmKeyFrames, 120.0);
            return exactTick + CurrentChart.Offset;
        }
    }
}