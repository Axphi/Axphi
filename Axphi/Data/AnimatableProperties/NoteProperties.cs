using Axphi.Data;

namespace Axphi.Data.AnimatableProperties
{
    public class NoteProperties : IProperties
    {

        public Property<NoteKind> Kind { get; } = new();
        public Property<ChartVector> Anchor { get; } = new();
        public Property<ChartVector> Position { get; } = new();

        public Property<ChartVector> Scale { get; } = new() { InitialValue = new ChartVector(1, 1) }; // 列入非法属性
        public Property<double> Rotation { get; } = new();
        public Property<double> Opacity { get; } = new() { InitialValue = 100}; // Note 的透明度也是非法的

        public Property<double> Speed { get; } = new() { InitialValue = 1 };
    }
}
