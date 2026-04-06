using Axphi.Data.AnimatableProperties;

namespace Axphi.Data.KeyFrames
{
    public class BpmKeyFrame : KeyFrame<BpmProperty, double>
    {
        public BpmKeyFrame() { Value = 120; }
    }
}
