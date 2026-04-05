namespace Axphi.Data.KeyFrames
{
    public class KeyFrame<TParent, T> : RelationObject<TParent>
        where TParent : class
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

}
