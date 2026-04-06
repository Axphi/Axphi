using Axphi.Data.Abstraction;

namespace Axphi.Data.KeyFrames
{
    public class OpacityKeyFrame<TParent> : KeyFrame<TParent, double>, IFloat64KeyFrame
        where TParent : class
    {
        public OpacityKeyFrame() { Value = 1; }
    }

}
