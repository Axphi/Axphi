using Axphi.Data.KeyFrames;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class OpacityProperty : AnimatableProperty<double, OpacityKeyFrame>
    {
        public OpacityProperty()
        {
            InitialValue = 1;
        }
    }
}
