namespace Axphi.Data.KeyFrames
{
    public record class OpacityKeyFrame : KeyFrameBase
    {
        /// <summary>
        /// 不透明度 (0~1)
        /// </summary>
        public double Opacity { get; set; } = 1;
    }
}
