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

        public Note(NoteKind kind, TimeSpan hitTime)
        {
            Kind = kind;
            HitTime = hitTime;
        }

        public NoteKind Kind { get; set; }
        public TimeSpan HitTime { get; set; }
        public TimeSpan HoldDuration { get; set; }
        public double? CustomSpeed { get; set; }

        public Vector InitialOffset { get; set; }
        public Vector InitialScale { get; set; } = new Vector(1, 1);
        public double InitialRotation { get; set; }
        public double InitialOpacity { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public TransformKeyFrames? TransformKeyFrames { get; set; }
    }
}
