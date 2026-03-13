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
using Axphi.Utilities; // 引入 EasingUtils 所在的命名空间

namespace Axphi.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {

        // 1. 保留原来的字段，这样你下面的代码（比如 _timeline.GetCurrentTick()）都不用改！
        public TimelineViewModel _timeline;

        // 🌟 2. 新增一个公开属性，专门给 XAML 界面绑定用！
        public TimelineViewModel Timeline => _timeline;

        // 【新增】免死金牌标志位
        private bool _isSyncing = false;

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
        // ================= 5. 核心拦截器：双向绑定的终极魔法 =================
        // ----- Position (Offset) -----
        partial void OnCurrentOffsetXChanged(double value)
        {
            // 如果是播放器在推着数值走，千万别动！
            if (_isSyncing) return;
            // 核心：不管三七二十一，只要人类动手了，立刻大喊一声“刹车！”
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            // 如果是人手在拖拽：直接调用我们写好的“添加/修改关键帧”逻辑！
            // 智能判断：如果完全没有关键帧，只修改基础值
            if (Data.AnimatableProperties.Offset.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Offset.InitialValue = new Vector(CurrentOffsetX, CurrentOffsetY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddPositionKeyframe();
            }
        }

        partial void OnCurrentOffsetYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Offset.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Offset.InitialValue = new Vector(CurrentOffsetX, CurrentOffsetY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddPositionKeyframe();
            }
        }

        // ----- Scale -----
        partial void OnCurrentScaleXChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Scale.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Scale.InitialValue = new Vector(CurrentScaleX, CurrentScaleY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddScaleKeyframe();
            }
        }

        partial void OnCurrentScaleYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Scale.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Scale.InitialValue = new Vector(CurrentScaleX, CurrentScaleY);
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddScaleKeyframe();
            }
        }

        // ----- Rotation -----
        partial void OnCurrentRotationChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Rotation.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Rotation.InitialValue = CurrentRotation;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddRotationKeyframe();
            }
        }

        // ----- Opacity -----
        partial void OnCurrentOpacityChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            if (Data.AnimatableProperties.Opacity.KeyFrames.Count == 0)
            {
                Data.AnimatableProperties.Opacity.InitialValue = CurrentOpacity;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
            else
            {
                AddOpacityKeyframe();
            }
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

            

            if (existingWrapper != null)
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

                // 【核心修复】：每次添加完新的，立刻让底层 List 按时间 (Time) 重新排队！
                offsetKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));

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
            var scaleKeyframesData = Data.AnimatableProperties.Scale.KeyFrames;
            var existingWrapper = UIScaleKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = new Vector(CurrentScaleX, CurrentScaleY);
            }
            else
            {
                // 假设你有一个 ScaleKeyFrame 继承自 KeyFrame<Vector>
                var newFrame = new ScaleKeyFrame() { Time = currentTick, Value = new Vector(CurrentScaleX, CurrentScaleY) };
                scaleKeyframesData.Add(newFrame);
                scaleKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Rotation 命令 =================
        [RelayCommand]
        private void AddRotationKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var rotationKeyframesData = Data.AnimatableProperties.Rotation.KeyFrames;
            var existingWrapper = UIRotationKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentRotation;
            }
            else
            {
                // 假设你有 RotationKeyFrame 继承自 KeyFrame<double>
                var newFrame = new RotationKeyFrame() { Time = currentTick, Value = CurrentRotation };
                rotationKeyframesData.Add(newFrame);
                rotationKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // ================= Opacity 命令 =================
        [RelayCommand]
        private void AddOpacityKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var opacityKeyframesData = Data.AnimatableProperties.Opacity.KeyFrames;
            var existingWrapper = UIOpacityKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentOpacity;
            }
            else
            {
                // 假设你有 OpacityKeyFrame 继承自 KeyFrame<double>
                var newFrame = new OpacityKeyFrame() { Time = currentTick, Value = CurrentOpacity };
                opacityKeyframesData.Add(newFrame);
                opacityKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // 【新增】暴露给大管家调用的同步方法
        public void SyncValuesToTime(int currentTick, KeyFrameEasingDirection direction)
        {
            _isSyncing = true; // 举起免死金牌：现在是机器在更新数据，不是人在拖拽！

            // 召唤你底层写好的算力神兽！直接白嫖它的插值结果！
            EasingUtils.CalculateObjectTransform(
                currentTick,
                direction,
                Data.AnimatableProperties,
                out var offset, out var scale, out var rotationAngle, out var opacity);

            // 把算出来的精确数值，直接塞给前端 UI 绑定的属性！
            CurrentOffsetX = offset.X;
            CurrentOffsetY = offset.Y;
            CurrentScaleX = scale.X;
            CurrentScaleY = scale.Y;
            CurrentRotation = rotationAngle;
            CurrentOpacity = opacity;

            _isSyncing = false; // 放下免死金牌
        }
    }
}

