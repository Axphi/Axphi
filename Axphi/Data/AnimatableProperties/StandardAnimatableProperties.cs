using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.Data.AnimatableProperties
{
    public class StandardAnimatableProperties
    {
        public AnchorProperty Anchor { get; } = new AnchorProperty();
        public OffsetProperty Offset { get; } = new OffsetProperty();
        public ScaleProperty Scale { get; } = new ScaleProperty();
        public RotationProperty Rotation { get; } = new RotationProperty();
        public OpacityProperty Opacity { get; } = new OpacityProperty();
    }
}
