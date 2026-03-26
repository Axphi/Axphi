namespace Axphi.Data.KeyFrames
{
    public record class KeyFrame<T>
        where T : struct
    {
        /// <summary>
        /// 时间
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// 从此关键帧到下一个关键帧之间的插值方式
        /// </summary>
        public BezierEasing Easing { get; set; } = Axphi.Utilities.BezierPresets.Linear;

        /// <summary>
        /// 是否为定格关键帧。为 true 时，该关键帧之后到下一个关键帧前保持常量值。
        /// </summary>
        public bool IsFreezeKeyframe { get; set; }

        /// <summary>
        /// 关键帧值
        /// </summary>
        public T Value { get; set; }
    }

}
