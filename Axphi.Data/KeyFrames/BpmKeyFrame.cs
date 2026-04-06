using Axphi.Data.Abstraction;
using Axphi.Data.AnimatableProperties;

namespace Axphi.Data.KeyFrames
{
    public class BpmKeyFrame : KeyFrame<BpmProperty, double>, IFloat64KeyFrame
    {
        public BpmKeyFrame() { Value = 120; }
    }
}
