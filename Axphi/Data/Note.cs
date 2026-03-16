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
            InitialKind = kind;
            HitTime = hitTime;
        }
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = string.Empty;
        public NoteKind InitialKind { get; set; } = NoteKind.Tap;
        public List<NoteKindKeyFrame> KindKeyFrames { get; set; } = new();
        public int HitTime { get; set; }
        public int HoldDuration { get; set; } = 100;
        public double? CustomSpeed { get; set; }

        /// <summary>
        /// 动画属性
        /// </summary>
        public StandardAnimatableProperties AnimatableProperties { get; } = new StandardAnimatableProperties();
    }
}
