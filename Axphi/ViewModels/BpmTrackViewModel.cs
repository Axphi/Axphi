using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using System.Drawing;


namespace Axphi.ViewModels
{
    public partial class BpmTrackViewModel : ObservableObject
    {
        private readonly Chart _chart;
        
        // 1. 保留原来的字段，这样你下面的代码（比如 _timeline.GetCurrentTick()）都不用改！
        public TimelineViewModel _timeline;

        // 🌟 2. 新增一个公开属性，专门给 XAML 界面绑定用！
        public TimelineViewModel Timeline => _timeline;

        // 又是我们熟悉的免死金牌！
        private bool _isSyncing = false;

        // 绑定到 UI 上的菱形集合 (复用咱们之前写的泛型包装器，简直完美)
        public ObservableCollection<KeyFrameUIWrapper<double>> UIBpmKeyframes { get; } = new();

        // 绑定到左侧 DraggableValueBox 的当前 BPM 值
        [ObservableProperty]
        private double _currentBpm;

        public BpmTrackViewModel(Chart chart, TimelineViewModel timeline)
        {
            _chart = chart;
            _timeline = timeline;

            // 【修改 1 继续】软件启动时，读取全局的 InitialBpm
            CurrentBpm = _chart.InitialBpm;

            // 软件启动时，把底层已有的 BPM 关键帧转换成 UI 菱形
            if (_chart.BpmKeyFrames != null)
            {
                foreach (var kf in _chart.BpmKeyFrames)
                {
                    UIBpmKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));
                }
            }
            WeakReferenceMessenger.Default.Register<BpmTrackViewModel, KeyframesNeedSortMessage>(this, (r, m) =>
            {
                r._chart.BpmKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
            });
        }

        // ================= 1. 播放时的被动同步 (UI 跟着数据跑) =================
        public void SyncValuesToTime(int currentTick)
        {
            _isSyncing = true;

            // 直接白嫖咱们上一回合写的神级工具类！自带幽灵帧保护！
            // 注意: 此函数的关键帧插值为 constant
            // 【修改 2】把硬编码的 120.0 替换为 _chart.InitialBpm，让物理引擎和 UI 彻底统一
            CurrentBpm = KeyFrameUtils.GetStepValueAtTick(_chart.BpmKeyFrames, currentTick, _chart.InitialBpm);

            _isSyncing = false;
        }

        // ================= 2. 拖拽时的主动拦截 (数据跟着人手跑) =================
        partial void OnCurrentBpmChanged(double value)
        {
            if (_isSyncing) return;

            // 人类动手了，立刻刹车！
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

            // 🌟 动手前，死死记住当前的精确网格位置
            double currentExactTick = _timeline.GetExactTick();

            // 【修改 3】智能拦截：判断是否完全没有 BPM 关键帧
            if (_chart.BpmKeyFrames.Count == 0)
            {
                // 1. 没有关键帧，单纯修改全局的初始 BPM
                _chart.InitialBpm = value;


                // 动手后，去修正时间！
                // 否则, 游标会突变
                SyncPlayheadToTick(currentExactTick);


                // ================= 🌟 补上这句：通知音频轨道刷新 =================
                _timeline.AudioTrack?.UpdatePixels();

            }
            else
            {
                // 已经有关键帧了，走添加/覆盖逻辑
                AddBpmKeyframe();
            }
        }

        // ================= 3. 生成/修改关键帧的绝对核心 =================
        [RelayCommand]
        private void AddBpmKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var existingWrapper = UIBpmKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                // 如果当前时间已经有菱形了，疯狂修改它的值！
                existingWrapper.Model.Value = CurrentBpm;
            }
            else
            {
                // 如果没有，砸下一个新的！
                // (注意：如果你底层的类叫 BpmKeyFrame，请把这里改成 new BpmKeyFrame() )
                var newFrame = new KeyFrame<double>() { Time = currentTick, Value = CurrentBpm };

                _chart.BpmKeyFrames.Add(newFrame);

                // ✨ 极其重要的排序！保证底层插值和寻址绝对正确
                _chart.BpmKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));

                UIBpmKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }

            // 通知渲染器重绘 (因为 BPM 变了，所有判定线的位置都会跟着变！)
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());

            // ================= 🌟 补上这句：通知音频轨道刷新 =================
            _timeline.AudioTrack?.UpdatePixels();
        }

        private void SyncPlayheadToTick(double targetExactTick)
        {
            double relativeTick = targetExactTick - _chart.Offset;
            if (relativeTick < 0) relativeTick = 0;

            // 反推算出新的物理秒数
            double newSeconds = TimeTickConverter.TickToTime(relativeTick, _chart.BpmKeyFrames, _chart.InitialBpm);

            // 1. 给大管家（游标）
            _timeline.CurrentPlayTimeSeconds = newSeconds;

            // 🌟 2. 寄信给渲染器和音频播放器：强行空降到新的秒数！
            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(newSeconds));

            // 3. 通知画面重绘
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

    }


}