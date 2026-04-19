using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System.Text.Json.Serialization;
using System.Windows;

namespace Axphi.Data
{
    /// <summary>
    /// 判定线
    /// </summary>
    public class JudgementLine
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string? ParentLineId { get; set; }
        public string? Name { get; set; } = null;
        public int StartTick { get; set; } = 0;
        public int DurationTicks { get; set; } = 7680; // 默认给个长度

        /// <summary>
        /// 动画属性
        /// </summary>
        public LineProperties Properties { get; } = new();

        // 默认使用实时(Realtime)模式，也可选积分(Integral)模式
        public string SpeedMode { get; set; } = "Realtime";

        /// <summary>
        /// 音符
        /// </summary>
        public List<Note> Notes { get; set; } = new();
    }
}
