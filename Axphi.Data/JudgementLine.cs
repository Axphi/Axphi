using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 判定线
    /// </summary>
    public class JudgementLine
    {
        public int ID { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public double Speed { get; set; } = 1;

        /// <summary>
        /// 动画属性
        /// </summary>
        public StandardAnimatableProperties AnimatableProperties { get; } = new();

        /// <summary>
        /// 音符
        /// </summary>
        public List<Note> Notes { get; set; } = new();
    }
}
