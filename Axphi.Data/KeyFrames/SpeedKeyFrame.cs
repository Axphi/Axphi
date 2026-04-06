using Axphi.Data.Abstraction;

namespace Axphi.Data.KeyFrames
{
    public class SpeedKeyFrame<TParent> : KeyFrame<TParent, double>, IFloat64KeyFrame
        where TParent : class
    {
        public SpeedKeyFrame() { Value = 1; }
    }

}
