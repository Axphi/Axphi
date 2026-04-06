using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Axphi.ViewModels
{
    public partial class NoteViewModel : ObservableObject, ISelectionNode, ITimelineDraggable
    {
        public Note Model { get; }
        private readonly TimelineViewModel _timeline;
        public TrackViewModel ParentTrack { get; }

        private bool _isSyncing = false; // 免死金牌


        // ================= 1. 音符的基础属性 (与底层 Model 双向绑定) =================
        public string NoteName
        {
            get => Model.Name;
            set => SetProperty(Model.Name, value, Model, (m, v) => m.Name = v);
        }

        

        public int HitTime
        {
            get => Model.HitTime;
            set
            {
                if (SetProperty(Model.HitTime, value, Model, (m, v) => m.HitTime = v))
                {
                    UpdatePosition(); // 时间改变时，同步更新 UI 位置
                }
            }
        }

        // ================= 2. 纯 UI 状态 =================
        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            _timeline.RefreshNoteSelectionState(ParentTrack, value ? this : null);
            _timeline.RefreshLayerSelectionVisuals();
        }

        bool ISelectionNode.IsSelected
        {
            get => IsSelected;
            set => IsSelected = value;
        }

        [ObservableProperty]
        private bool _isExpanded; // 音符的属性面板是否展开

        [ObservableProperty]
        private double _pixelX;

        // 供 XAML 绑定的长按持续时间（Tick）
        [ObservableProperty]
        private int _holdDuration;

        // 供 XAML 长条矩形绑定的物理像素宽度！
        [ObservableProperty]
        private double _uIHoldPixelWidth;

        // ================= 3. 音符【专属】的动画 UI 替身集合 =================
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIAnchorKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIOffsetKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<Vector>> UIScaleKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<double>> UIRotationKeyframes { get; } = new();
        public ObservableCollection<KeyFrameUIWrapper<double>> UIOpacityKeyframes { get; } = new();

        public ObservableCollection<KeyFrameUIWrapper<NoteKind>> UINoteKindKeyframes { get; } = new();

        // ================= 4. 供 XAML 绑定的【音符专属】当前数值 =================
        [ObservableProperty] private double _currentAnchorX;
        [ObservableProperty] private double _currentAnchorY;
        [ObservableProperty] private double _currentOffsetX;
        [ObservableProperty] private double _currentOffsetY;
        [ObservableProperty] private double _currentScaleX = 1.0;
        [ObservableProperty] private double _currentScaleY = 1.0;
        [ObservableProperty] private double _currentRotation;
        [ObservableProperty] private double _currentOpacity = 100.0;
        // 专供 UI 左侧属性面板绑定的当前音符种类
        [ObservableProperty]
        private NoteKind _currentNoteKind;

        [ObservableProperty]
        private bool _hasCustomSpeed;

        [ObservableProperty]
        private double _currentCustomSpeed = 1.0;


        // 构造函数
        public NoteViewModel(Note model, TimelineViewModel timeline, TrackViewModel parentTrack)
        {
            Model = model;
            _timeline = timeline;
            ParentTrack = parentTrack;
            UpdatePosition();
            HoldDuration = Model.HoldDuration;
            HasCustomSpeed = Model.CustomSpeed.HasValue;
            CurrentCustomSpeed = Model.CustomSpeed ?? 1.0;


            // === 把底层已有的数据全部包上保镖 ===
            if (Model.AnimatableProperties.Anchor.KeyFrames != null)
                foreach (var kf in Model.AnimatableProperties.Anchor.KeyFrames)
                    UIAnchorKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Model.AnimatableProperties.Offset.KeyFrames != null)
                foreach (var kf in Model.AnimatableProperties.Offset.KeyFrames)
                    UIOffsetKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Model.AnimatableProperties.Scale.KeyFrames != null)
                foreach (var kf in Model.AnimatableProperties.Scale.KeyFrames)
                    UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(kf, _timeline));

            if (Model.AnimatableProperties.Rotation.KeyFrames != null)
                foreach (var kf in Model.AnimatableProperties.Rotation.KeyFrames)
                    UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));

            if (Model.AnimatableProperties.Opacity.KeyFrames != null)
                foreach (var kf in Model.AnimatableProperties.Opacity.KeyFrames)
                    UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(kf, _timeline));

            if (Model.KindKeyFrames != null)
                foreach (var kf in Model.KindKeyFrames)
                    UINoteKindKeyframes.Add(new KeyFrameUIWrapper<NoteKind>(kf, _timeline));

            // TODO: 这里可以保留我们之前写的接收 NotesDragStartedMessage 等拖拽逻辑


            // ================= 监听协同拖拽广播 =================
            // 收到起手式：如果是被选中的，且不是自己发起的，就准备跟着动！
            WeakReferenceMessenger.Default.Register<NoteViewModel, NotesDragStartedMessage>(this, (r, m) =>
            {
                if (r.IsSelected && !ReferenceEquals(r, m.SenderToIgnore)) r.ReceiveDragStarted();
            });

            // 收到位移量：跟着挪动！
            WeakReferenceMessenger.Default.Register<NoteViewModel, NotesDragDeltaMessage>(this, (r, m) =>
            {
                if (r.IsSelected && !ReferenceEquals(r, m.SenderToIgnore)) r.ReceiveDragDelta(m.HorizontalChange);
            });

            // 收到收尾：更新最终位置
            WeakReferenceMessenger.Default.Register<NoteViewModel, NotesDragCompletedMessage>(this, (r, m) =>
            {
                // 别人发起的，传 false
                if (r.IsSelected && !ReferenceEquals(r, m.SenderToIgnore)) r.ReceiveDragCompleted(false);
            });

            // 监听清除选中广播
            WeakReferenceMessenger.Default.Register<NoteViewModel, ClearSelectionMessage>(this, (recipient, message) =>
            {
                if (message.GroupName == "Notes" && !ReferenceEquals(recipient, message.SenderToIgnore))
                {
                    recipient.IsSelected = false; // 乖乖熄灭
                }
            });


            // 在 NoteViewModel 的构造函数里加上这行：// 监听全局的 ZoomScaleChangedMessage，一旦收到就调用 UpdatePosition() 更新位置
            WeakReferenceMessenger.Default.Register<NoteViewModel, ZoomScaleChangedMessage>(this, (r, m) => r.UpdatePosition());

            // 跨频道监听：如果普通关键帧发起了拖拽，选中的音符也跟着动！
            WeakReferenceMessenger.Default.Register<NoteViewModel, KeyframesDragStartedMessage>(this, (r, m) => { if (r.IsSelected) r.ReceiveDragStarted(); });
            WeakReferenceMessenger.Default.Register<NoteViewModel, KeyframesDragDeltaMessage>(this, (r, m) => { if (r.IsSelected) r.ReceiveDragDelta(m.HorizontalChange); });
            WeakReferenceMessenger.Default.Register<NoteViewModel, KeyframesDragCompletedMessage>(this, (r, m) => { if (r.IsSelected) r.ReceiveDragCompleted(false); });
        }

        private void UpdatePosition()
        {
            PixelX = _timeline.TickToPixel(Model.HitTime);
            // 🌟 新增：缩放时，Hold 尾巴的像素宽度也要跟着按比例伸缩！
            UIHoldPixelWidth = _timeline.TickToPixel(Model.HoldDuration);
        }

        private void UpdateDisplayedValues(Action updateAction)
        {
            _isSyncing = true;
            updateAction();
            _isSyncing = false;
        }

        private void UpsertPositionKeyframe(double x, double y)
        {
            int currentTick = _timeline.GetCurrentTick();
            var offsetKeyframesData = Model.AnimatableProperties.Offset.KeyFrames;
            var existingWrapper = UIOffsetKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = new Vector(x, y);
            }
            else
            {
                var newFrame = new OffsetKeyFrame() { Time = currentTick, Value = new Vector(x, y) };
                offsetKeyframesData.Add(newFrame);
                offsetKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIOffsetKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline));
            }
        }

        private void UpsertAnchorKeyframe(double x, double y)
        {
            int currentTick = _timeline.GetCurrentTick();
            var anchorKeyframesData = Model.AnimatableProperties.Anchor.KeyFrames;
            var existingWrapper = UIAnchorKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = new Vector(x, y);
            }
            else
            {
                var newFrame = new OffsetKeyFrame() { Time = currentTick, Value = new Vector(x, y) };
                anchorKeyframesData.Add(newFrame);
                anchorKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIAnchorKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline));
            }
        }

        private void UpsertScaleKeyframe(double x, double y)
        {
            int currentTick = _timeline.GetCurrentTick();
            var scaleKeyframesData = Model.AnimatableProperties.Scale.KeyFrames;
            var existingWrapper = UIScaleKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = new Vector(x, y);
            }
            else
            {
                var newFrame = new ScaleKeyFrame() { Time = currentTick, Value = new Vector(x, y) };
                scaleKeyframesData.Add(newFrame);
                scaleKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIScaleKeyframes.Add(new KeyFrameUIWrapper<Vector>(newFrame, _timeline));
            }
        }

        private void UpsertRotationKeyframe(double value)
        {
            int currentTick = _timeline.GetCurrentTick();
            var rotationKeyframesData = Model.AnimatableProperties.Rotation.KeyFrames;
            var existingWrapper = UIRotationKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = value;
            }
            else
            {
                var newFrame = new RotationKeyFrame() { Time = currentTick, Value = value };
                rotationKeyframesData.Add(newFrame);
                rotationKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIRotationKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
        }

        private void UpsertOpacityKeyframe(double value)
        {
            int currentTick = _timeline.GetCurrentTick();
            var opacityKeyframesData = Model.AnimatableProperties.Opacity.KeyFrames;
            var existingWrapper = UIOpacityKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = value;
            }
            else
            {
                var newFrame = new OpacityKeyFrame() { Time = currentTick, Value = value };
                opacityKeyframesData.Add(newFrame);
                opacityKeyframesData.Sort((a, b) => a.Time.CompareTo(b.Time));
                UIOpacityKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }
        }

        private void UpsertNoteKindKeyframe(NoteKind value)
        {
            int currentTick = _timeline.GetCurrentTick();
            var existingWrapper = UINoteKindKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = value;
            }
            else
            {
                var newFrame = new NoteKindKeyFrame() { Time = currentTick, Value = value };
                Model.KindKeyFrames.Add(newFrame);
                Model.KindKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
                UINoteKindKeyframes.Add(new KeyFrameUIWrapper<NoteKind>(newFrame, _timeline));
            }
        }

        private void ApplyPositionChangeInternal(double x, double y)
        {
            if (Model.AnimatableProperties.Offset.KeyFrames.Count == 0)
            {
                Model.AnimatableProperties.Offset.InitialValue = new Vector(x, y);
            }
            else
            {
                UpsertPositionKeyframe(x, y);
            }

            UpdateDisplayedValues(() =>
            {
                CurrentOffsetX = x;
                CurrentOffsetY = y;
            });
        }

        public void ApplyPositionAbsolute(double x, double y)
        {
            ApplyPositionChangeInternal(x, y);
        }

        public void ApplyPositionDelta(double deltaX, double deltaY)
        {
            ApplyPositionChangeInternal(CurrentOffsetX + deltaX, CurrentOffsetY + deltaY);
        }

        private void ApplyAnchorChangeInternal(double x, double y)
        {
            if (Model.AnimatableProperties.Anchor.KeyFrames.Count == 0)
            {
                Model.AnimatableProperties.Anchor.InitialValue = new Vector(x, y);
            }
            else
            {
                UpsertAnchorKeyframe(x, y);
            }

            UpdateDisplayedValues(() =>
            {
                CurrentAnchorX = x;
                CurrentAnchorY = y;
            });
        }

        public void ApplyAnchorAbsolute(double x, double y)
        {
            ApplyAnchorChangeInternal(x, y);
        }

        public void ApplyAnchorDelta(double deltaX, double deltaY)
        {
            ApplyAnchorChangeInternal(CurrentAnchorX + deltaX, CurrentAnchorY + deltaY);
        }

        private void ApplyScaleChangeInternal(double x, double y)
        {
            if (Model.AnimatableProperties.Scale.KeyFrames.Count == 0)
            {
                Model.AnimatableProperties.Scale.InitialValue = new Vector(x, y);
            }
            else
            {
                UpsertScaleKeyframe(x, y);
            }

            UpdateDisplayedValues(() =>
            {
                CurrentScaleX = x;
                CurrentScaleY = y;
            });
        }

        public void ApplyScaleAbsolute(double x, double y)
        {
            ApplyScaleChangeInternal(x, y);
        }

        public void ApplyScaleDelta(double deltaX, double deltaY)
        {
            ApplyScaleChangeInternal(CurrentScaleX + deltaX, CurrentScaleY + deltaY);
        }

        private void ApplyRotationChangeInternal(double value)
        {
            if (Model.AnimatableProperties.Rotation.KeyFrames.Count == 0)
            {
                Model.AnimatableProperties.Rotation.InitialValue = value;
            }
            else
            {
                UpsertRotationKeyframe(value);
            }

            UpdateDisplayedValues(() => CurrentRotation = value);
        }

        public void ApplyRotationAbsolute(double value)
        {
            ApplyRotationChangeInternal(value);
        }

        public void ApplyRotationDelta(double delta)
        {
            ApplyRotationChangeInternal(CurrentRotation + delta);
        }

        private void ApplyOpacityChangeInternal(double value)
        {
            if (Model.AnimatableProperties.Opacity.KeyFrames.Count == 0)
            {
                Model.AnimatableProperties.Opacity.InitialValue = value;
            }
            else
            {
                UpsertOpacityKeyframe(value);
            }

            UpdateDisplayedValues(() => CurrentOpacity = value);
        }

        public void ApplyOpacityAbsolute(double value)
        {
            ApplyOpacityChangeInternal(value);
        }

        public void ApplyOpacityDelta(double delta)
        {
            ApplyOpacityChangeInternal(Math.Clamp(CurrentOpacity + delta, 0.0, 100.0));
        }

        private void ApplyCustomSpeedChangeInternal(bool hasCustomSpeed, double customSpeed)
        {
            Model.CustomSpeed = hasCustomSpeed ? customSpeed : null;

            UpdateDisplayedValues(() =>
            {
                HasCustomSpeed = hasCustomSpeed;
                CurrentCustomSpeed = customSpeed;
            });
        }

        public void ApplyHasCustomSpeed(bool hasCustomSpeed, double customSpeed)
        {
            ApplyCustomSpeedChangeInternal(hasCustomSpeed, customSpeed);
        }

        public void ApplyCustomSpeedAbsolute(double value)
        {
            ApplyCustomSpeedChangeInternal(true, value);
        }

        public void ApplyCustomSpeedDelta(double delta)
        {
            ApplyCustomSpeedChangeInternal(true, CurrentCustomSpeed + delta);
        }

        private void ApplyNoteKindChangeInternal(NoteKind value)
        {
            if (Model.KindKeyFrames == null || Model.KindKeyFrames.Count == 0)
            {
                Model.InitialKind = value;
            }
            else
            {
                UpsertNoteKindKeyframe(value);
            }

            UpdateDisplayedValues(() => CurrentNoteKind = value);
        }

        public void ApplyNoteKindAbsolute(NoteKind value)
        {
            ApplyNoteKindChangeInternal(value);
        }

        // ================= 5. 核心拦截器 (当你在面板上拖拽音符的属性时触发) =================
        partial void OnCurrentOffsetXChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyPositionChangeInternal(CurrentOffsetX, CurrentOffsetY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentAnchorXChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyAnchorChangeInternal(CurrentAnchorX, CurrentAnchorY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentAnchorYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyAnchorChangeInternal(CurrentAnchorX, CurrentAnchorY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentOffsetYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyPositionChangeInternal(CurrentOffsetX, CurrentOffsetY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentScaleXChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyScaleChangeInternal(CurrentScaleX, CurrentScaleY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentScaleYChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyScaleChangeInternal(CurrentScaleX, CurrentScaleY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentRotationChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyRotationChangeInternal(CurrentRotation);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
        partial void OnCurrentOpacityChanged(double value)
        {
            if (_isSyncing) return;
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyOpacityChangeInternal(CurrentOpacity);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnHasCustomSpeedChanged(bool value)
        {
            if (_isSyncing) return;

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyCustomSpeedChangeInternal(value, CurrentCustomSpeed);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentCustomSpeedChanged(double value)
        {
            if (_isSyncing || !HasCustomSpeed) return;

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            ApplyCustomSpeedChangeInternal(true, value);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }


        // 当用户在属性面板里手动修改了数字时，同步更新底层 Model 和前端宽度
        partial void OnHoldDurationChanged(int value)
        {
            if (value < 1)
            {
                if (HoldDuration != 1)
                {
                    HoldDuration = 1;
                }
                return;
            }

            if (Model != null)
            {
                Model.HoldDuration = value;
                UIHoldPixelWidth = _timeline.TickToPixel(value);
                // 发送重绘广播，让渲染器里的 9 宫格尾巴也跟着伸长！
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());

                // 2. 🌟 核心新增：尾巴变长变短了，可能会撞到别人！立刻通知大管家重新排车道！
                WeakReferenceMessenger.Default.Send(new NotesNeedSortMessage());
            }
        }

        partial void OnCurrentNoteKindChanged(NoteKind value)
        {
            if (_isSyncing) return;

            // ================= 🌟 终极防御装甲 =================
            // 防御 WPF 控件的延迟绑定回传：验证是不是真实的用户修改！
            if (Model.KindKeyFrames != null && Model.KindKeyFrames.Count > 0)
            {
                int currentTick = _timeline.GetCurrentTick();
                var expectedKind = KeyFrameUtils.GetStepValueAtTick(Model.KindKeyFrames, currentTick, Model.InitialKind);

                // 如果传回来的值，和系统算出来的当前真实值一样，说明是幽灵回传，直接无视！
                if (value == expectedKind) return;
            }
            // =================================================


            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

            ApplyNoteKindChangeInternal(value);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        [RelayCommand]
        private void AddNoteKindKeyframe()
        {
            UpsertNoteKindKeyframe(CurrentNoteKind);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        












        // ... (Scale, Rotation, Opacity 的拦截器和 AddKeyframe 逻辑与 TrackVM 完全一样，只是把 Data 换成 Model，这里为了简洁略过重复代码，你直接粘贴过来改名即可) ...

        [RelayCommand]
        private void AddAnchorKeyframe()
        {
            UpsertAnchorKeyframe(CurrentAnchorX, CurrentAnchorY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        [RelayCommand]
        private void AddPositionKeyframe()
        {
            UpsertPositionKeyframe(CurrentOffsetX, CurrentOffsetY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        [RelayCommand]
        private void AddScaleKeyframe()
        {
            UpsertScaleKeyframe(CurrentScaleX, CurrentScaleY);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
        [RelayCommand]
        private void AddRotationKeyframe()
        {
            UpsertRotationKeyframe(CurrentRotation);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
        [RelayCommand]
        private void AddOpacityKeyframe()
            {
            UpsertOpacityKeyframe(CurrentOpacity);
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        // 给大管家调用的同步方法
        public void SyncValuesToTime(int currentTick, KeyFrameEasingDirection direction)
        {
            _isSyncing = true;
            EasingUtils.CalculateObjectTransform(
                currentTick, direction, Model.AnimatableProperties,
                out var anchor, out var offset, out var scale, out var rotationAngle, out var opacity);

            CurrentAnchorX = anchor.X;
            CurrentAnchorY = anchor.Y;
            CurrentOffsetX = offset.X;
            CurrentOffsetY = offset.Y;
            CurrentScaleX = scale.X;
            CurrentScaleY = scale.Y;
            CurrentRotation = rotationAngle;
            CurrentOpacity = opacity;
            _isSyncing = false;
            // ✨ 召唤阶跃函数，算出当前时间点它到底是个什么种类的音符！
            if (Model.KindKeyFrames != null)
            {
                CurrentNoteKind = KeyFrameUtils.GetStepValueAtTick(Model.KindKeyFrames, currentTick, Model.InitialKind);
            }
            else
            {
                CurrentNoteKind = Model.InitialKind;
            }
        }



















        // =======================================================
        // ================= 拖拽与多选核心逻辑 =================
        // =======================================================

        // 声明三个临时变量，用来记录拖拽轨迹
        private double _virtualPixelX;
        private double _dragAccumulated; // 记录鼠标到底挪动了多少距离
        private bool _wasSelectedBeforeDrag; // 记录按下鼠标前，它是不是已经被选中了

        // ====== 发起端：由 XAML 中的 Thumb 拖拽事件直接调用 ======

        public void OnDragStarted()
        {
            // 自己做好准备
            ReceiveDragStarted();

            _timeline.EnterSubItemSelectionContext(this);

            // 如果按下的是一个【尚未选中】的音符
            _wasSelectedBeforeDrag = SelectionHelper.BeginSelectionGesture("Notes", this, IsSelected, val => IsSelected = val);

            // 如果此时我是亮着的（选中状态），大喊一声：兄弟们，准备发车！
            if (IsSelected)
            {
                WeakReferenceMessenger.Default.Send(new NotesDragStartedMessage(this));
            }
        }

        public void OnDragDelta(double horizontalChange)
        {
            // 如果我被选中了，把位移量发给兄弟们
            if (IsSelected)
            {
                WeakReferenceMessenger.Default.Send(new NotesDragDeltaMessage(horizontalChange, this));
            }

            // 自己挪动
            ReceiveDragDelta(horizontalChange);
        }

        public void OnDragCompleted()
        {
            if (IsSelected)
            {
                WeakReferenceMessenger.Default.Send(new NotesDragCompletedMessage(this));
            }

            // 自己收尾（传 true，表示我是被鼠标直接捏住的那个“带头大哥”）
            ReceiveDragCompleted(true);
        }

        // ====== 接收端：处理实际的数值变化和广播响应 ======

        private void ReceiveDragStarted()
        {
            _virtualPixelX = PixelX;
            _dragAccumulated = 0;
        }

        // ================= 拖拽接收端 (处理实际的数值变化) =================
        private void ReceiveDragDelta(double horizontalChange)
        {
            _dragAccumulated += Math.Abs(horizontalChange);
            _virtualPixelX += horizontalChange;

            // 可以加一个限制，防止音符拖到负数时间
            if (_virtualPixelX < 0) _virtualPixelX = 0;

            // 1. 算出纯鼠标位置的绝对 Tick
            double exactTickDouble = _timeline.PixelToTick(_virtualPixelX);

            // 2. 🌟 召唤大管家的智能磁吸雷达！
            int newTick = _timeline.SnapToClosest(exactTickDouble);

            // 3. 只有数值真正变化了才修改底层和发重绘
            if (newTick != Model.HitTime)
            {
                // 直接修改底层 Model，防止触发 HitTime 的 setter 导致 PixelX 强制吸附回滚
                Model.HitTime = newTick;
                // 手动通知 UI 左侧面板里的 HitTime 数值更新
                OnPropertyChanged(nameof(HitTime));

                // 性能优化提示：这里每一帧都在发重绘广播
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }

            // 4. 【核心防闪烁】：UI 像素的更新必须放在最后，且只赋值一次！
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // 按了 Shift，UI 强行锁死在吸附点的像素上！绝对不许跟着鼠标乱动！
                PixelX = _timeline.TickToPixel(newTick);
            }
            else
            {
                // 没按 Shift，UI 才跟着鼠标丝滑走
                PixelX = _virtualPixelX;
            }
        }

        private void ReceiveDragCompleted(bool isInitiator)
        {
            // 只有被鼠标直接捏住的那个“带头大哥”，才有资格处理单击取消选中的判定
            if (isInitiator && _wasSelectedBeforeDrag && _dragAccumulated < 2.0)
            {
                SelectionHelper.CompleteSelectionGesture("Notes", this, _wasSelectedBeforeDrag, _dragAccumulated, val => IsSelected = val, () => _timeline.ClearKeyframeSelection());
            }

            // 拖拽松手后，强制吸附对齐到准确的 Tick 像素位置
            UpdatePosition();

            // 发送音符重排信号，让 TrackViewModel 把底层 Note List 重新按时间排个序
            WeakReferenceMessenger.Default.Send(new NotesNeedSortMessage());
        }




        // 记录音符当前在第几条子轨道 (0代表第一条，1代表第二条...)
        [ObservableProperty]
        private int _laneIndex;

        // 供 XAML 绑定的垂直物理像素坐标
        [ObservableProperty]
        private double _pixelY;

        // 当车道改变时，自动计算它的物理高度！
        partial void OnLaneIndexChanged(int value)
        {
            // 假设每一条子轨道的高度是 24 像素 (给点上下间距，原来是 20)
            PixelY = value * 24;
        }





        // 音符本体及内部关键帧平移 API
        public void ShiftBy(int deltaTick)
        {
            Model.HitTime += deltaTick;
            // if (Model.HitTime < 0) Model.HitTime = 0; // 防越界

            // 手动触发 UI 左侧面板里的 HitTime 数值更新
            OnPropertyChanged(nameof(HitTime));

            // 🌟 核心：让自己肚子里的关键帧也跟着搬家！
            foreach (var kf in UIOffsetKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIAnchorKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIScaleKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIRotationKeyframes) kf.ShiftBy(deltaTick);
            foreach (var kf in UIOpacityKeyframes) kf.ShiftBy(deltaTick);
            if (UINoteKindKeyframes != null)
                foreach (var kf in UINoteKindKeyframes) kf.ShiftBy(deltaTick);

            // 更新音符本体在轨道上的像素位置
            PixelX = _timeline.TickToPixel(Model.HitTime);
        }
    }
}