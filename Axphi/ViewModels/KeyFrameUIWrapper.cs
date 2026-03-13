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
        private double _virtualPixelX;

        public void OnDragStarted()
        {
            _virtualPixelX = PixelX;
        }

        public void OnDragDelta(double horizontalChange)
        {
            // 1. 累加真实的拖拽距离
            _virtualPixelX += horizontalChange;
            if (_virtualPixelX < 0) _virtualPixelX = 0;

            // 🌟 核心破局点：不要吸附 UI！让小菱形的像素坐标丝滑地跟着鼠标走！
            // 这样 WPF 就永远不会算错相对位移，彻底根除频闪！
            PixelX = _virtualPixelX;

            // 2. 暗中算出底层应该吸附的整数 Tick
            double exactTick = _timeline.PixelToTick(_virtualPixelX);
            int newTick = (int)Math.Round(exactTick, MidpointRounding.AwayFromZero);

            // 3. 只要 Tick 跨格了，就更新数据并重绘画面
            if (newTick != Model.Time)
            {
                Model.Time = newTick;
                // 注意：这里我们故意不调用 UpdatePosition() 干扰 UI！

                // 告诉右侧的渲染器：数据变了，画面请“吧嗒吧嗒”地吸附过去！
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            }
        }

        public void OnDragCompleted()
        {
            // 🌟 拖拽松手时：鼠标放开了，我们再调用 UpdatePosition，
            // 把小菱形强行吸附回正规的 Tick 网格线上！
            UpdatePosition();

            // 告诉大管家重新排队
            WeakReferenceMessenger.Default.Send(new KeyframesNeedSortMessage());
        }
    }
}