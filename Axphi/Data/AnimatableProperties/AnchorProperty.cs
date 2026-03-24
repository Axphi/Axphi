using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class AnchorProperty : AnimatableProperty<Vector, OffsetKeyFrame>
    {
        public AnchorProperty()
        {
            InitialValue = new Vector(0, 0);
        }
    }
}
