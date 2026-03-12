using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Axphi.ViewModels
{
    public partial class KeyFrameUIWrapper : ObservableObject
    {
        // 1. 手里紧紧抓着底层那个纯净的实体
        public OffsetKeyFrame Model { get; }

        // 2. 认识大管家，为了找它要换算公式
        private readonly TimelineViewModel _timeline;

        // 3. 这是专供 XAML Canvas.Left 绑定的纯物理坐标！
        [ObservableProperty]
        private double _pixelX;

        public KeyFrameUIWrapper(OffsetKeyFrame model, TimelineViewModel timeline)
        {
            Model = model;
            _timeline = timeline;

            // 刚出生时，算一次自己的绝对位置
            UpdatePosition();

            // 监听大管家的缩放广播！只要一缩放，自己就重新算位置！
            WeakReferenceMessenger.Default.Register<KeyFrameUIWrapper, ZoomScaleChangedMessage>(this, (recipient, message) =>
            {
                recipient.UpdatePosition();
            });
        }

        // 核心计算逻辑：要计算像素坐标，就去找管家问
        private void UpdatePosition()
        {
            PixelX = _timeline.TickToPixel(Model.Time);
        }
    }
}