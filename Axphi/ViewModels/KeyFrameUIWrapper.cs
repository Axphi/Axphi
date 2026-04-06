using Axphi.Data.KeyFrames;
using Axphi.Data;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;

namespace Axphi.ViewModels
{
    public interface IKeyFrameUiItem
    {
        bool IsSelected { get; }
        bool IsFreezeKeyframe { get; set; }
        void ApplyEasing(BezierEasing easing);
    }

    // 加上 <T>，这样无论是 Vector 还是 double 都能包！
    public partial class KeyFrameUIWrapper<T> : ObservableObject, IKeyFrameUiItem, ISelectionNode, ITimelineDraggable, IRightClickableTimelineItem where T : struct
    {
        private const string KeyframeSelectionGroup = "Keyframes";

        public KeyFrame<T> Model { get; }
        private readonly TimelineViewModel _timeline;

        // 1. 核心状态：是否被选中
        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            _timeline.RefreshLayerSelectionVisuals();
        }

        bool ISelectionNode.IsSelected
        {
            get => IsSelected;
            set => IsSelected = value;
        }

        [ObservableProperty]
        private double _pixelX;

        public int Time => Model.Time;

        public bool IsFreezeKeyframe
        {
            get => Model.IsFreezeKeyframe;
            set
            {
                if (Model.IsFreezeKeyframe == value)
                {
                    return;
                }

                Model.IsFreezeKeyframe = value;
                OnPropertyChanged(nameof(IsFreezeKeyframe));
            }
        }

        public void ApplyEasing(BezierEasing easing)
        {
            Model.Easing = easing;
        }



        public KeyFrameUIWrapper(KeyFrame<T> model, TimelineViewModel timeline)
        {
            Model = model;
            _timeline = timeline;
            UpdatePosition();

            RegisterMessageHandlers();
        }

        private void RegisterMessageHandlers()
        {
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, ZoomScaleChangedMessage>(this, (recipient, message) =>
            {
                recipient.UpdatePosition();
            });

            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, ClearSelectionMessage>(this, (recipient, message) =>
            {
                if (message.GroupName == KeyframeSelectionGroup && !ReferenceEquals(recipient, message.SenderToIgnore))
                {
                    recipient.IsSelected = false;
                }
            });

            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, KeyframesDragStartedMessage>(this, (r, m) =>
            {
                r.TryReceiveDragStarted(m.SenderToIgnore);
            });

            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, KeyframesDragDeltaMessage>(this, (r, m) =>
            {
                r.TryReceiveDragDelta(m.HorizontalChange, m.SenderToIgnore);
            });

            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, KeyframesDragCompletedMessage>(this, (r, m) =>
            {
                r.TryReceiveDragCompleted(m.SenderToIgnore);
            });

            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, NotesDragStartedMessage>(this, (r, m) => r.TryReceiveDragStarted());
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, NotesDragDeltaMessage>(this, (r, m) => r.TryReceiveDragDelta(m.HorizontalChange));
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, NotesDragCompletedMessage>(this, (r, m) => r.TryReceiveDragCompleted());
        }

        private void TryReceiveDragStarted(object? senderToIgnore = null)
        {
            if (!IsSelected || ReferenceEquals(this, senderToIgnore))
            {
                return;
            }

            ReceiveDragStarted();
        }

        private void TryReceiveDragDelta(double horizontalChange, object? senderToIgnore = null)
        {
            if (!IsSelected || ReferenceEquals(this, senderToIgnore))
            {
                return;
            }

            ReceiveDragDelta(horizontalChange);
        }

        private void TryReceiveDragCompleted(object? senderToIgnore = null)
        {
            if (!IsSelected || ReferenceEquals(this, senderToIgnore))
            {
                return;
            }

            ReceiveDragCompleted(false);
        }

        private void UpdatePosition()
        {
            PixelX = _timeline.TickToPixel(Model.Time);
        }


        // [!!!] 此函数未曾被调用和 Binding
        //// 核心命令：点击小菱形时触发
        //[RelayCommand]
        //private void ToggleSelection()
        //{
        //    // 直接白嫖刚刚写的帮助类！
        //    SelectionHelper.HandleSelection("Keyframes", this, IsSelected, val => IsSelected = val);
        //}


        // 声明一个临时变量，记录拖拽时真实的物理像素（防止鼠标挪动太慢，吸附后丢失精度）
        
        // 声明三个临时变量，用来记录拖拽轨迹
        private double _virtualPixelX;
        private double _dragAccumulated; // 记录鼠标到底挪动了多少距离
        private bool _wasSelectedBeforeDrag; // 记录按下鼠标前，它是不是已经被选中了

        public void OnDragStarted()
        {
            // 自己也得做好准备
            ReceiveDragStarted();

            _timeline.EnterSubItemSelectionContext(this);

            

            // 如果没被选中，按下的瞬间立刻点亮它！
            // 如果按下的是一个【尚未选中】的关键帧
            _wasSelectedBeforeDrag = SelectionHelper.BeginSelectionGesture(KeyframeSelectionGroup, this, IsSelected, val => IsSelected = val);

            // 如果此时我是亮着的（选中状态），大喊一声：兄弟们，准备发车！
            if (IsSelected)
            {
                WeakReferenceMessenger.Default.Send(new KeyframesDragStartedMessage(this));
            }

            
        }

        public void OnDragDelta(double horizontalChange)
        {
            // 如果我被选中了，把位移量发给兄弟们
            if (IsSelected)
            {
                WeakReferenceMessenger.Default.Send(new KeyframesDragDeltaMessage(horizontalChange, this));
            }

            // 自己挪动
            ReceiveDragDelta(horizontalChange);
        }

        public void OnDragCompleted()
        {
            if (IsSelected)
            {
                WeakReferenceMessenger.Default.Send(new KeyframesDragCompletedMessage(this));
            }

            // 自己收尾（传 true，表示我是被鼠标直接捏住的那个“带头大哥”）
            ReceiveDragCompleted(true);

        }

        public void OnRightClick()
        {
            if (!_timeline.IsTrackLevelKeyframeWrapperSelected(this))
            {
                return;
            }

            _timeline.EnterSubItemSelectionContext(this);
            SelectionHelper.HandleSelection(KeyframeSelectionGroup, this, IsSelected, value => IsSelected = value);
            _timeline.ClearNoteSelection();

            if (_timeline.GetSelectedTrackLevelKeyframeCount() <= 0)
            {
                _timeline.RefreshLayerSelectionVisuals();
                return;
            }

            bool targetFreezeState = !IsFreezeKeyframe;
            _timeline.SetFreezeStateForSelectedTrackLevelKeyframes(targetFreezeState);

            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            _timeline.RefreshLayerSelectionVisuals();
        }

        // ================= 拖拽接收端 (处理实际的数值变化) =================
        private void ReceiveDragStarted()
        {
            _virtualPixelX = PixelX;
            _dragAccumulated = 0;
        }

        private void ReceiveDragDelta(double horizontalChange)
        {
            _dragAccumulated += Math.Abs(horizontalChange);
            _virtualPixelX += horizontalChange;

            // 1. 算出纯鼠标位置的绝对 Tick
            double exactTickDouble = _timeline.PixelToTick(_virtualPixelX);

            // 2. 🌟 召唤大管家的智能磁吸雷达！
            int newTick = _timeline.SnapToClosest(exactTickDouble);

            // 3. 只有数值真正变化了才修改底层和发重绘
            if (newTick != Model.Time)
            {
                SetModelTime(newTick);
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
                SelectionHelper.CompleteSelectionGesture(KeyframeSelectionGroup, this, _wasSelectedBeforeDrag, _dragAccumulated, val => IsSelected = val, () => _timeline.ClearNoteSelection());
            }

            UpdatePosition();
            WeakReferenceMessenger.Default.Send(new KeyframesNeedSortMessage());
        }




        // 极简平移 API
        public void ShiftBy(int deltaTick)
        {
            SetModelTime(Model.Time + deltaTick);
            // if (Model.Time < 0) Model.Time = 0; // 物理防撞墙：绝不能退到 0 以前
            UpdatePosition(); // 调用你原本就有的方法，重新算像素！
        }

        private void SetModelTime(int newTime)
        {
            if (Model.Time == newTime)
            {
                return;
            }

            Model.Time = newTime;
            OnPropertyChanged(nameof(Time));
        }
    }
}