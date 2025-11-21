namespace Axphi.Data
{
    public abstract record class KeyFrameBase
    {
        /// <summary>
        /// 时间
        /// </summary>
        public TimeSpan Time { get; set; }

        /// <summary>
        /// 从上一个 BPM 关键帧之间的插值方式
        /// </summary>
        public BezierEasing Easing { get; set; } = BezierEasing.Linear;
    }
}
