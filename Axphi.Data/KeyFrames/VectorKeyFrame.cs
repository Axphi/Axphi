using System.Windows;

namespace Axphi.Data.KeyFrames
{
    public record class VectorKeyFrame : KeyFrameBase
    {
        /// <summary>
        /// 向量值, 可做位置, 也可做偏移量
        /// </summary>
        public Vector Vector { get; set; }
    }
}
