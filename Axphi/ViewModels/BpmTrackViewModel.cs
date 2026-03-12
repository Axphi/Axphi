using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;

namespace Axphi.ViewModels
{
    public partial class BpmTrackViewModel : ObservableObject
    {
        private readonly Chart _chart;
        private readonly TimelineViewModel _timeline;

        // 又是我们熟悉的免死金牌！
        private bool _isSyncing = false;

        // 绑定到 UI 上的菱形集合 (复用咱们之前写的泛型包装器，简直完美)
        public ObservableCollection<KeyFrameUIWrapper<double>> UIBpmKeyframes { get; } = new();

        // 绑定到左侧 DraggableValueBox 的当前 BPM 值
        [ObservableProperty]
        private double _currentBpm = 120.0;

        public BpmTrackViewModel(Chart chart, TimelineViewModel timeline)
        {
            _chart = chart;
            _timeline = timeline;

            // 软件启动时，把底层已有的 BPM 关键帧转换成 UI 菱形
            if (_chart.BpmKeyFrames != null)
            {
                foreach (var kf in _chart.BpmKeyFrames)
                {
                    UIBpmKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));
                }
            }
        }

        // ================= 1. 播放时的被动同步 (UI 跟着数据跑) =================
        public void SyncValuesToTime(int currentTick)
        {
            _isSyncing = true;

            // 直接白嫖咱们上一回合写的神级工具类！自带幽灵帧保护！
            CurrentBpm = KeyFrameUtils.GetStepValueAtTick(_chart.BpmKeyFrames, currentTick, 120.0);

            _isSyncing = false;
        }

        // ================= 2. 拖拽时的主动拦截 (数据跟着人手跑) =================
        partial void OnCurrentBpmChanged(double value)
        {
            if (_isSyncing) return;

            // 人类动手了，立刻刹车！
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            AddBpmKeyframe();
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
        }
    }
}