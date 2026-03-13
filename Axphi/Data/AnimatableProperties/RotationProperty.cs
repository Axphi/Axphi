using Axphi.Data.KeyFrames;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class RotationProperty : AnimatableProperty<double, RotationKeyFrame>
    {
        public RotationProperty()
        {
            InitialValue = 0;
        }
    }

}
