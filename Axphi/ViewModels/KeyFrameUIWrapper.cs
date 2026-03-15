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

            // ================= 监听清除选中广播 =================
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, ClearSelectionMessage>(this, (recipient, message) =>
            {
                // 如果大家都在 "Keyframes" 这个频道，且这封信不是我（自己）发的
                if (message.GroupName == "Keyframes" && !ReferenceEquals(recipient, message.SenderToIgnore))
                {
                    recipient.IsSelected = false; // 乖乖熄灭
                }
            });

            // ================= 新增：监听协同拖拽广播 =================
            // 收到起手式：如果是被选中的，且不是自己发起的，就准备跟着动！
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, KeyframesDragStartedMessage>(this, (r, m) =>
            {
                if (r.IsSelected && !ReferenceEquals(r, m.SenderToIgnore)) r.ReceiveDragStarted();
            });

            // 收到位移量：跟着挪动！
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, KeyframesDragDeltaMessage>(this, (r, m) =>
            {
                if (r.IsSelected && !ReferenceEquals(r, m.SenderToIgnore)) r.ReceiveDragDelta(m.HorizontalChange);
            });

            // 收到收尾：更新最终位置
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper<T>, KeyframesDragCompletedMessage>(this, (r, m) =>
            {
                if (r.IsSelected && !ReferenceEquals(r, m.SenderToIgnore)) r.ReceiveDragCompleted(false); // 别人发起的，传 false
            });
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

            

            // 如果没被选中，按下的瞬间立刻点亮它！
            // 如果按下的是一个【尚未选中】的关键帧
            if (!IsSelected)
            {
                // 走标准的多选/单选逻辑
                SelectionHelper.HandleSelection("Keyframes", this, IsSelected, val => IsSelected = val);
            }

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

        // ================= 拖拽接收端 (处理实际的数值变化) =================
        private void ReceiveDragStarted()
        {
            _virtualPixelX = PixelX;
            _dragAccumulated = 0;
            _wasSelectedBeforeDrag = IsSelected;
        }

        private void ReceiveDragDelta(double horizontalChange)
        {
            _dragAccumulated += Math.Abs(horizontalChange);

            _virtualPixelX += horizontalChange;
            // if (_virtualPixelX < 0) _virtualPixelX = 0; // 防止拖到 0 以前

            PixelX = _virtualPixelX;

            double exactTick = _timeline.PixelToTick(_virtualPixelX);
            int newTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);

            if (newTick != Model.Time)
            {
                Model.Time = newTick;
                // 性能优化提示：这里每一帧都在发重绘广播。因为是协同拖拽，如果选中了 10 个关键帧，
                // 就会在同一毫秒内发 10 次重绘消息。好在你的 ChartDisplay 已经处理了性能问题。
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
        }

        private void ReceiveDragCompleted(bool isInitiator)
        {
            // 只有被鼠标直接捏住的那个“带头大哥”，才有资格处理单击取消选中的判定
            if (isInitiator && _wasSelectedBeforeDrag && _dragAccumulated < 2.0)
            {
                bool isCtrlDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                bool isShiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                if (isCtrlDown)
                    IsSelected = false; // Ctrl+单击：取消选中自己
                else if (isShiftDown)
                    IsSelected = true;  // Shift+单击：保持不变
                else
                {
                    // 普通单击：排他选中自己
                    WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Keyframes", this));

                    // 2. 🌟 新增：清空所有音符（取消选中音符本体）
                    WeakReferenceMessenger.Default.Send(new ClearSelectionMessage("Notes", null));
                    IsSelected = true;
                }
            }

            UpdatePosition();
            WeakReferenceMessenger.Default.Send(new KeyframesNeedSortMessage());
        }

    }
}