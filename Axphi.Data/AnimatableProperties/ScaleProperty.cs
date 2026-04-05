using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class ScaleProperty<TOwner> :
        AnimatableProperty<TOwner, ScaleProperty<TOwner>, Vector, ScaleKeyFrame<ScaleProperty<TOwner>>>
        where TOwner : class
    {
        public ScaleProperty(TOwner owner) : base(owner)
        {
            InitialValue = new Vector(1, 1);
        }
    }
}
