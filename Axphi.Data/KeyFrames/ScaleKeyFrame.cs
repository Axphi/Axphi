using System.Windows;

namespace Axphi.Data.KeyFrames
{
    public record class ScaleKeyFrame : KeyFrameBase
    {
        /// <summary>
        /// XY 方向上的缩放
        /// </summary>
        public Vector Scale { get; set; } = new Vector(1, 1);
    }
}
