using Axphi.Data;

namespace Axphi.Data.AnimatableProperties
{
    
    public interface IProperties
    {
        Property<ChartVector> Anchor { get; }
        Property<ChartVector> Position { get; }
        Property<ChartVector> Scale { get; }
        Property<double> Rotation { get; }
        Property<double> Opacity { get; }
    }
}