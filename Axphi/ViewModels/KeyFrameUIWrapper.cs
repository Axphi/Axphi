using Axphi.Data.KeyFrames;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Axphi.ViewModels
{
    // 加上 <T>，这样无论是 Vector 还是 double 都能包！
    public partial class KeyFrameUIWrapper<T> : ObservableObject where T : struct
    {
        public KeyFrame<T> Model { get; }
        private readonly TimelineViewModel _timeline;

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
    }
}