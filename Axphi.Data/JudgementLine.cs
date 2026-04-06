using Axphi.Data.Abstraction;
using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 判定线
    /// </summary>
    public class JudgementLine : RelationObject<Chart>, IWithStandardAnimatableProperties
    {
        public string? Name { get; set; }

        #region Animatable Properties
        public OffsetProperty<JudgementLine> Offset { get; }
        public ScaleProperty<JudgementLine> Scale { get; }
        public RotationProperty<JudgementLine> Rotation { get; }
        public OpacityProperty<JudgementLine> Opacity { get; }
        public SpeedProperty<JudgementLine> Speed { get; }
        #endregion


        #region Impl ICanTransform

        IAnimatableProperty<Vector> IWithStandardAnimatableProperties.Offset => Offset;
        IAnimatableProperty<Vector> IWithStandardAnimatableProperties.Scale => Scale;
        IAnimatableProperty<double> IWithStandardAnimatableProperties.Rotation => Rotation;
        IAnimatableProperty<double> IWithStandardAnimatableProperties.Opacity => Opacity;

        #endregion

        /// <summary>
        /// 音符
        /// </summary>
        public RelationObject<JudgementLine>.Collection<Note> Notes { get; }

        public JudgementLine()
        {
            Offset = new OffsetProperty<JudgementLine>(this);
            Scale = new ScaleProperty<JudgementLine>(this);
            Rotation = new RotationProperty<JudgementLine>(this);
            Opacity = new OpacityProperty<JudgementLine>(this);
            Speed = new SpeedProperty<JudgementLine>(this);
            Notes = new RelationObject<JudgementLine>.Collection<Note>(this);
        }
    }
}
