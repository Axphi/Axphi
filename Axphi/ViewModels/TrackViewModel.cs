using Axphi.Data;
using Axphi.Data.KeyFrames; // 引入你的 OffsetKeyFrame
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 用来发重绘消息
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {

        // 保存引用
        private readonly TimelineViewModel _timeline;



        // ================= 1. 底层数据源 =================
        // 这个属性是只读的，它紧紧抓住那个不会被污染的底层老实人
        public JudgementLine Data { get; }

        // ================= 2. 纯 UI 状态（不参与 JSON 导出） =================
        [ObservableProperty]
        private bool _isExpanded; // 记录左侧的属性面板是否展开（v 和 >）

        [ObservableProperty]
        private string _trackName; // 轨道的名字，比如 "判定线 1"

        // ================= 3. 供 DraggableValueBox 绑定的双向数据 =================
        [ObservableProperty]
        private double _currentOffsetX;

        [ObservableProperty]
        private double _currentOffsetY;

        [ObservableProperty]
        private double _currentScaleX = 1.0; // 默认缩放给 1

        [ObservableProperty]
        private double _currentScaleY = 1.0;

        [ObservableProperty]
        private double _currentRotation;

        [ObservableProperty]
        private double _currentOpacity = 1.0; // 默认透明度给 1

        // ================= 4. 构造函数 =================
        public TrackViewModel(JudgementLine data, string name,TimelineViewModel timeline)
        {
            Data = data;
            TrackName = name;
            _timeline = timeline;
        }

        // ================= 5. 核心拦截器 (黑魔法) =================
        // 当你在界面上按住 DraggableValueBox 左右拖拽时，这个方法会被疯狂触发！
        partial void OnCurrentOffsetXChanged(double value)
        {
            // TODO: 我们下一阶段的核心逻辑将写在这里：
            // 1. 获取当前时间轴播放到了哪一秒 (Tick)
            // 2. 去 Data 里面找当前时间有没有关键帧
            // 3. 如果有，直接修改那个关键帧的 X 值
            // 4. 如果没有，就 new 一个新的关键帧塞进去！

            // 可以先打个日志测试一下
            System.Diagnostics.Debug.WriteLine($"轨道 {TrackName} 的 OffsetX 被拖拽成了: {value}");
        }

        
        

        // ================= 添加 Offset (Position) 关键帧 =================
        [RelayCommand]
        private void AddPositionKeyframe()
        {
            // 1. 问爸爸：现在是第几个 Tick？
            int currentTick = _timeline.GetCurrentTick();

            // 2. 顺藤摸瓜，拿到你底层数据里的 Offset KeyFrames 集合
            var offsetKeyframes = Data.AnimatableProperties.Offset.KeyFrames;

            // 3. 找找看当前 Tick 是不是已经有关键帧了
            var existingFrame = offsetKeyframes.FirstOrDefault(k => k.Time == currentTick);

            if (existingFrame != null)
            {
                // 如果有，直接修改它的值
                existingFrame.Value = new Vector(CurrentOffsetX, CurrentOffsetY);
                System.Diagnostics.Debug.WriteLine($"修改了 Tick {currentTick} 处的 Offset 关键帧");
            }
            else
            {
                // 如果没有，New 一个新的存进去！
                var newFrame = new OffsetKeyFrame()
                {
                    Time = currentTick,
                    Value = new Vector(CurrentOffsetX, CurrentOffsetY)
                };

                // 加进 ObservableCollection！未来绑定 UI 极其方便！
                offsetKeyframes.Add(newFrame);
                System.Diagnostics.Debug.WriteLine($"新建了 Tick {currentTick} 处的 Offset 关键帧");
            }

            // 4. 发广播通知右侧的 ChartRenderer 重新画一下谱面
            // (借用一下你之前写的 JudgementLinesChangedMessage)
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
    }
}

