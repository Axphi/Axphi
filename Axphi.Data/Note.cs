using Axphi.Data.Abstraction;
using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 音符
    /// </summary>
    public class Note : RelationObject<JudgementLine>, IWithStandardAnimatableProperties
    {
        public NoteKind Kind { get; set; }
        public TimeSpan HitTime { get; set; }
        public TimeSpan HoldDuration { get; set; }
        public double? CustomSpeed { get; set; }

        #region Animatable Properties
        public OffsetProperty<Note> Offset { get; }
        public ScaleProperty<Note> Scale { get; }
        public RotationProperty<Note> Rotation { get; }
        public OpacityProperty<Note> Opacity { get; }

        #endregion

        #region Impl ICanTransform

        IAnimatableProperty<Vector> IWithStandardAnimatableProperties.Offset => Offset;
        IAnimatableProperty<Vector> IWithStandardAnimatableProperties.Scale => Scale;
        IAnimatableProperty<double> IWithStandardAnimatableProperties.Rotation => Rotation;
        IAnimatableProperty<double> IWithStandardAnimatableProperties.Opacity => Opacity;

        #endregion

        public Note()
        {
            Offset = new OffsetProperty<Note>(this);
            Scale = new ScaleProperty<Note>(this);
            Rotation = new RotationProperty<Note>(this);
            Opacity = new OpacityProperty<Note>(this);
        }

        public Note(NoteKind kind, TimeSpan hitTime) : this()
        {
            Kind = kind;
            HitTime = hitTime;
        }
    }
}
