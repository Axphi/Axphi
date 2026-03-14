using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;

namespace Axphi.ViewModels
{
    // 加上 <T>，这样无论是 Vector 还是 double 都能包！
    public partial class KeyFrameUIWrapper<T> : ObservableObject where T : struct
    {
        public KeyFrame<T> Model { get; }
        private readonly TimelineViewModel _timeline;

        // 1. 核心状态：是否被选中
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private double _pixelX;

        public KeyFrameUIWrapper(KeyFrame<T> model, TimelineViewModel timeline)
        {
            Model = model;
            _timeline = timeline;
            UpdatePosition();

            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, ZoomScaleChangedMessage>(this, (recipient, message) =>
            {
                recipient.UpdatePosition();
            });

            // ================= 新增：监听清除选中广播 =================
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, ClearSelectionMessage>(this, (recipient, message) =>
            {
                // 如果大家都在 "Keyframes" 这个频道，且这封信不是我（自己）发的
                if (message.GroupName == "Keyframes" && !ReferenceEquals(recipient, message.SenderToIgnore))
                {
                    recipient.IsSelected = false; // 乖乖熄灭
                }
            });
        }

        private void UpdatePosition()
        {
            PixelX = _timeline.TickToPixel(Model.Time);
        }

        // 2. 核心命令：点击小菱形时触发
        [RelayCommand]
        private void ToggleSelection()
        {
            // 直接白嫖刚刚写的帮助类！
            SelectionHelper.HandleSelection("Keyframes", this, IsSelected, val => IsSelected = val);
        }


        // 声明一个临时变量，记录拖拽时真实的物理像素（防止鼠标挪动太慢，吸附后丢失精度）
        
        // 声明三个临时变量，用来记录拖拽轨迹
        private double _virtualPixelX;
        private double _dragAccumulated; // 记录鼠标到底挪动了多少距离
        private bool _wasSelectedBeforeDrag; // 记录按下鼠标前，它是不是已经被选中了

        public void OnDragStarted()
        {
            _virtualPixelX = PixelX;
            _dragAccumulated = 0; // 拖拽距离清零
            _wasSelectedBeforeDrag = IsSelected; // 记住按下前的状态

            // 如果没被选中，按下的瞬间立刻点亮它！
            // 如果按下的是一个【尚未选中】的关键帧
            if (!IsSelected)
            {
                // 走标准的多选/单选逻辑
                SelectionHelper.HandleSelection("Keyframes", this, IsSelected, val => IsSelected = val);
            }
        }

        public void OnDragDelta(double horizontalChange)
        {
            // 累加鼠标移动的绝对距离
            _dragAccumulated += Math.Abs(horizontalChange);

            // 依然是那套无敌防频闪的平滑拖拽代码
            _virtualPixelX += horizontalChange;
            if (_virtualPixelX < 0) _virtualPixelX = 0;

            PixelX = _virtualPixelX;

            double exactTick = _timeline.PixelToTick(_virtualPixelX);
            int newTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);

            if (newTick != Model.Time)
            {
                Model.Time = newTick;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
        }

        public void OnDragCompleted()
        {
            // 如果按下去之前它就是亮的，并且完全没拖动（位移<2，是个纯点击）
            if (_wasSelectedBeforeDrag && _dragAccumulated < 2.0)
            {
                bool isCtrlDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                bool isShiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                if (isCtrlDown)
                {
                    IsSelected = false; // Ctrl + 单击已选中的帧：取消选中
                }
                else if (isShiftDown)
                {
                    IsSelected = true;  // Shift + 单击已选中的帧：保持加选状态
                }
                else
                {
                    // 什么都没按 + 单击已选中的帧：独占选中！（排他）
                    WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", this));
                    IsSelected = true;
                }
            }

            UpdatePosition();
            WeakReferenceMessenger.Default.Send(new KeyframesNeedSortMessage());
        }

        // 在 KeyFrameUIWrapper<T> 类中找个空地加上这段代码：
        partial void OnIsSelectedChanged(bool value)
        {
            // 只有当它变成被选中状态 (true) 时，我们才发广播。
            // 如果是取消选中，我们就忽略。
            if (value)
            {
                // 连同底层的真实 Easing 数据一起发出去！
                WeakReferenceMessenger.Default.Send(new KeyframeSelectedMessage(Model.Easing));
            }
        }
    }
}