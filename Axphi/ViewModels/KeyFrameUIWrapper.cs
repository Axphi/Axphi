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
    }
}