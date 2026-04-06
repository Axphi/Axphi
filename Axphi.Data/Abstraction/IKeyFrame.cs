using System.Windows;

namespace Axphi.Data.Abstraction
{
    public interface IKeyFrame<TValue>
    {
        public TimeSpan Time { get; set; }
        public BezierEasing? Easing { get; set; }
        public TValue Value { get; set; }
    }

    public interface IVectorKeyFrame : IKeyFrame<Vector>;
    public interface IFloat64KeyFrame : IKeyFrame<double>;
}
