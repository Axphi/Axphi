using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    
    public interface IProperties
    {
        Property<Vector> Anchor { get; }
        Property<Vector> Position { get; }
        Property<Vector> Scale { get; }
        Property<double> Rotation { get; }
        Property<double> Opacity { get; }
    }
}