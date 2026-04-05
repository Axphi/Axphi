using Axphi.Data.KeyFrames;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class RotationProperty<TOwner>(TOwner owner) :
        AnimatableProperty<TOwner, RotationProperty<TOwner>, double, RotationKeyFrame<RotationProperty<TOwner>>>(owner)
        where TOwner : class;
}
