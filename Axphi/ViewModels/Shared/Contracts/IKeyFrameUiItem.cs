using Axphi.Data;

namespace Axphi.ViewModels
{
    public interface IKeyFrameUiItem
    {
        bool IsSelected { get; }
        bool IsFreezeKeyframe { get; set; }
        void ApplyEasing(BezierEasing easing);
    }
}
