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

        public Note(NoteKind kind, int hitTime)
        {
            HitTime = hitTime;
            Properties.Kind.InitialValue = kind;
        }
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = string.Empty;
        public int HitTime { get; set; }
        private int _holdDuration = 128;
        public int HoldDuration
        {
            get => _holdDuration;
            set => _holdDuration = Math.Max(1, value);
        }
        
        public NoteProperties Properties { get; } = new NoteProperties();
    }
}
