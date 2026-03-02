using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class ScaleProperty : AnimatableProperty<Vector, ScaleKeyFrame>
    {
        
        public ScaleProperty() : base(new Vector(1, 1))
        {
        }
    }
}
