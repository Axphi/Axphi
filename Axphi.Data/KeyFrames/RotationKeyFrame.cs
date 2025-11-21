namespace Axphi.Data.KeyFrames
{
    public record class RotationKeyFrame : KeyFrameBase
    {
        /// <summary>
        /// 旋转量 (角度制)
        /// </summary>
        public double Angle { get; set; }
    }
}
