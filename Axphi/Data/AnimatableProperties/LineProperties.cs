using System.Windows;

namespace Axphi.Data.AnimatableProperties
{
    public class LineProperties : IProperties
    {
        public Property<Vector> Anchor { get; } = new();
        public Property<Vector> Position { get; } = new();

        public Property<Vector> Scale { get; } = new() { InitialValue = new Vector(1, 1) }; // 列入非法属性
        public Property<double> Rotation { get; } = new();
        public Property<double> Opacity { get; } = new() { InitialValue = 100 };

        public Property<double> Speed { get; } = new() { InitialValue = 1 };
    }
}
