using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 判定线
    /// </summary>
    public class JudgementLine
    {
        public string? Name { get; set; }
        public double Speed { get; set; } = 1;

        public Vector InitialOffset { get; set; }
        public Vector InitialScale { get; set; } = new Vector(1, 1);
        public double InitialRotation { get; set; }
        public double InitialOpacity { get; set; } = 1;

        public TransformKeyFrames? TransformKeyFrames { get; set; }

        /// <summary>
        /// 音符
        /// </summary>
        public List<Note>? Notes { get; set; }
    }
}
