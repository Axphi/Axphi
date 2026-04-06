using Axphi.Data.KeyFrames;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class BpmProperty :
        AnimatableProperty<Chart, BpmProperty, double, BpmKeyFrame>
    {
        public BpmProperty(Chart owner) : base(owner)
        {
            InitialValue = 120;
        }
    }
}
