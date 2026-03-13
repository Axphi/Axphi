using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 音符
    /// </summary>
    public class Note
    {
        public Note()
        {
        }

        public Note(NoteKind kind, int hitTime)
        {
            Kind = kind;
            HitTime = hitTime;
        }

        public NoteKind Kind { get; set; }
        public int HitTime { get; set; }
        public int HoldDuration { get; set; }
        public double? CustomSpeed { get; set; }

        /// <summary>
        /// 动画属性
        /// </summary>
        public StandardAnimatableProperties AnimatableProperties { get; } = new StandardAnimatableProperties();
    }
}
