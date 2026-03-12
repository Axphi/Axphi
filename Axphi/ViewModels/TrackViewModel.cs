using Axphi.Data;
using Axphi.Data.KeyFrames; // 引入你的 OffsetKeyFrame
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 用来发重绘消息
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace Axphi.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {

        // 保存引用
        private readonly TimelineViewModel _timeline;

        // ================= 专供 UI 绑定的小菱形替身集合 =================
        // 把原来直接装底层 KeyFrame 的集合，换成装保镖的集合
        // ================= 1. 声明四个 UI 替身集合 =================
        // Offset 和 Scale 是 X,Y 两个值，所以是 Vector
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIOffsetKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIScaleKeyframes { get; } = new();
        // Rotation 和 Opacity 只有一个值，所以是 double
        public ObservableCollection<KeyFrameUIWrapper<double>> UIRotationKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<double>> UIOpacityKeyframes { get; } = new();

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

            // 如果底层数据里已经有关键帧了，把它们请进 UI 替身集合里
            // 初始化时，把底层已有的关键帧全部包上一层保镖！
            // ================= 2. 构造时，把底层已有的数据全部包上保镖 =================
            if (Data.AnimatableProperties.Offset.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Offset.KeyFrames)
                    UIOffsetKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Data.AnimatableProperties.Scale.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Scale.KeyFrames)
                    UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Data.AnimatableProperties.Rotation.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Rotation.KeyFrames)
                    UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));

            if (Data.AnimatableProperties.Opacity.KeyFrames != null)
                foreach (var kf in Data.AnimatableProperties.Opacity.KeyFrames)
                    UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));
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
            var offsetKeyframesData = Data.AnimatableProperties.Offset.KeyFrames; // 底层的纯净 List

            // 重点：我们从保镖集合里去找有没有当前时间的
            var existingWrapper = UIOffsetKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            // 3. 找找看当前 Tick 是不是已经有关键帧了
            var existingFrame = offsetKeyframesData.FirstOrDefault(k => k.Time == currentTick);

            if (existingFrame != null)
            {
                // 如果有，直接修改它的值
                // 1. 修改底层 
                // 如果有了，直接修改保镖手里的那个底层 Model！
                existingWrapper.Model.Value = new System.Windows.Vector(CurrentOffsetX, CurrentOffsetY);
                
            }
            else
            {
                // 如果没有，New 一个底层的，再立刻给它配个保镖！
                var newFrame = new OffsetKeyFrame()
                {
                    Time = currentTick,
                    Value = new System.Windows.Vector(CurrentOffsetX, CurrentOffsetY)
                };

                offsetKeyframesData.Add(newFrame); // 存入底层
                UIOffsetKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline)); // 生成 UI 显示
            }

            // 4. 发广播通知右侧的 ChartRenderer 重新画一下谱面
            // (借用一下你之前写的 JudgementLinesChangedMessage)
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Scale 命令 =================
        [RelayCommand]
        private void AddScaleKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var keyframesData = Data.AnimatableProperties.Scale.KeyFrames;
            var existingWrapper = UIScaleKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = new Vector(CurrentScaleX, CurrentScaleY);
            }
            else
            {
                // 假设你有一个 ScaleKeyFrame 继承自 KeyFrame<Vector>
                var newFrame = new ScaleKeyFrame() { Time = currentTick, Value = new Vector(CurrentScaleX, CurrentScaleY) };
                keyframesData.Add(newFrame);
                UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Rotation 命令 =================
        [RelayCommand]
        private void AddRotationKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var keyframesData = Data.AnimatableProperties.Rotation.KeyFrames;
            var existingWrapper = UIRotationKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentRotation;
            }
            else
            {
                // 假设你有 RotationKeyFrame 继承自 KeyFrame<double>
                var newFrame = new RotationKeyFrame() { Time = currentTick, Value = CurrentRotation };
                keyframesData.Add(newFrame);
                UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Opacity 命令 =================
        [RelayCommand]
        private void AddOpacityKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var keyframesData = Data.AnimatableProperties.Opacity.KeyFrames;
            var existingWrapper = UIOpacityKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentOpacity;
            }
            else
            {
                // 假设你有 OpacityKeyFrame 继承自 KeyFrame<double>
                var newFrame = new OpacityKeyFrame() { Time = currentTick, Value = CurrentOpacity };
                keyframesData.Add(newFrame);
                UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
    }
}

