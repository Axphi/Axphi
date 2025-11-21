namespace Axphi.Data.KeyFrames
{
    public record class BpmKeyFrame : KeyFrameBase
    {
        /// <summary>
        /// BPM 值
        /// </summary>
        public double Bpm { get; set; }
    }
}
