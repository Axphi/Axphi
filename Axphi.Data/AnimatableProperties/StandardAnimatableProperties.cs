using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.Data.AnimatableProperties
{
    public class StandardAnimatableProperties<TOwner>
    {
        public TOwner Owner { get; }

        public OffsetProperty<StandardAnimatableProperties<TOwner>> Offset { get; }
        public ScaleProperty<StandardAnimatableProperties<TOwner>> Scale { get; }
        public RotationProperty<StandardAnimatableProperties<TOwner>> Rotation { get; }
        public OpacityProperty<StandardAnimatableProperties<TOwner>> Opacity { get; }

        public StandardAnimatableProperties(TOwner owner)
        {
            Owner = owner;
            Offset = new OffsetProperty<StandardAnimatableProperties<TOwner>>(this);
            Scale = new ScaleProperty<StandardAnimatableProperties<TOwner>>(this);
            Rotation = new RotationProperty<StandardAnimatableProperties<TOwner>>(this);
            Opacity = new OpacityProperty<StandardAnimatableProperties<TOwner>>(this);
        }
    }
}
