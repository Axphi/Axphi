using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Axphi.Data.Abstraction
{
    public interface IWithStandardAnimatableProperties
    {
        public IAnimatableProperty<Vector> Offset { get; }
        public IAnimatableProperty<Vector> Scale { get; }
        public IAnimatableProperty<double> Rotation { get; }
        public IAnimatableProperty<double> Opacity { get; }
    }
}
