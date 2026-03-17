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
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public double InitialSpeed { get; set; } = 1;

       public List<KeyFrame<double>> SpeedKeyFrames { get; set; } = new();

        // 默认使用实时(Realtime)模式，也可选积分(Integral)模式
        public string SpeedMode { get; set; } = "Realtime";

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
