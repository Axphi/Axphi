using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class OffsetProperty : AnimatableProperty<Vector, OffsetKeyFrame>
    {
        
        public OffsetProperty()
        {
            InitialValue = new Vector(0,0);
        }
    }
    

}
