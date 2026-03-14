using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

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
        }

        private void UpdatePosition()
        {
            PixelX = _timeline.TickToPixel(Model.Time);
        }

        // 2. 核心命令：点击小菱形时触发
        [RelayCommand]
        private void ToggleSelection()
        {
            // 切换选中状态 (True 变 False，False 变 True)
            IsSelected = !IsSelected;

            // 进阶提示：如果我们以后要做“单选”功能（点这个，其他自动取消），
            // 可以在这里发一个 Messenger 广播，让别的保镖把自己的 IsSelected 改成 false。
            // 目前我们先保持最简单的“点击反转”。
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

            // 🌟 智能判定 1：如果没被选中，按下的瞬间立刻点亮它！
            if (!IsSelected)
            {
                IsSelected = true;
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
            // 🌟 智能判定 2：如果按下去之前它就是蓝色的，而且我们松手时根本没挪动鼠标（位移小于 2 像素防手抖）
            // 这说明用户的真实意图是：单击取消选中！
            if (_wasSelectedBeforeDrag && _dragAccumulated < 2.0)
            {
                IsSelected = false;
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