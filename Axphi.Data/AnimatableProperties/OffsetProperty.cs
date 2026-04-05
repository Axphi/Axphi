using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public sealed class OffsetProperty<TOwner>(TOwner owner) :
        AnimatableProperty<TOwner, OffsetProperty<TOwner>, Vector, OffsetKeyFrame<OffsetProperty<TOwner>>>(owner)
        where TOwner : class;
}
