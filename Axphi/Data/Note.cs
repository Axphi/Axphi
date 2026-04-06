using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Text.Json.Serialization;
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
        private int _holdDuration = 128;
        public int HoldDuration
        {
            get => _holdDuration;
            set => _holdDuration = Math.Max(1, value);
        }
        public double? CustomSpeed { get; set; }

        [JsonIgnore]
        public JudgementLine? ParentLine { get; internal set; }

        /// <summary>
        /// 动画属性
        /// </summary>
        public StandardAnimatableProperties AnimatableProperties { get; } = new StandardAnimatableProperties();
    }
}
