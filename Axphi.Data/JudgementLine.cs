using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 判定线
    /// </summary>
    public class JudgementLine : RelationObject<Chart>
    {
        public string? Name { get; set; }
        public double Speed { get; set; } = 1;

        /// <summary>
        /// 动画属性
        /// </summary>
        public StandardAnimatableProperties<JudgementLine> AnimatableProperties { get; }

        /// <summary>
        /// 音符
        /// </summary>
        public RelationObject<JudgementLine>.Collection<Note> Notes { get; }

        public JudgementLine()
        {
            AnimatableProperties = new StandardAnimatableProperties<JudgementLine>(this);
            Notes = new RelationObject<JudgementLine>.Collection<Note>(this);
        }
    }
}
