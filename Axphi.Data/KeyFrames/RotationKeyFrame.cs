using Axphi.Data.Abstraction;

namespace Axphi.Data.KeyFrames
{
    public class RotationKeyFrame<TParent> : KeyFrame<TParent, double>, IFloat64KeyFrame
        where TParent : class;

}
