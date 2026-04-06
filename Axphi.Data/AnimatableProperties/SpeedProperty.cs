using Axphi.Data.KeyFrames;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class SpeedProperty<TOwner> :
        AnimatableProperty<TOwner, SpeedProperty<TOwner>, double, SpeedKeyFrame<SpeedProperty<TOwner>>>
        where TOwner : class
    {
        public SpeedProperty(TOwner owner) : base(owner)
        {
            InitialValue = 1;
        }
    }
}
