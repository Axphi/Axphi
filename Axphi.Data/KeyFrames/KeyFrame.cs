using System.Windows;

namespace Axphi.Data.KeyFrames
{
    public record class KeyFrame<T>
        where T : struct
    {
        /// <summary>
        /// 时间
        /// </summary>
        public TimeSpan Time { get; set; }

        /// <summary>
        /// 从上一个 BPM 关键帧之间的插值方式
        /// </summary>
        public BezierEasing? Easing { get; set; } = BezierEasing.Linear;

        /// <summary>
        /// 关键帧值
        /// </summary>
        public T Value { get; set; }
    }

    public record class OffsetKeyFrame : KeyFrame<Vector>;
    public record class ScaleKeyFrame : KeyFrame<Vector>
    {
        public ScaleKeyFrame() { Value = new Vector(1, 1); }
    }

    public record class RotationKeyFrame : KeyFrame<double>;

    public record class OpacityKeyFrame : KeyFrame<double>
    {
        public OpacityKeyFrame() { Value = 1; }
    }

}
