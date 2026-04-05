using Axphi.Data.KeyFrames;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class OpacityProperty<TOwner> :
        AnimatableProperty<TOwner, OpacityProperty<TOwner>, double, OpacityKeyFrame<OpacityProperty<TOwner>>>
        where TOwner : class
    {
        public OpacityProperty(TOwner owner) : base(owner)
        {
            InitialValue = 1;
        }
    }
}
