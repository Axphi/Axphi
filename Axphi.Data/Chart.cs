using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;

namespace Axphi.Data
{
    /// <summary>
    /// 谱面
    /// </summary>
    public class Chart
    {
        /// <summary>
        /// 音频偏移
        /// </summary>
        public TimeSpan Offset { get; set; }

        /// <summary>
        /// 时长
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 歌曲名称
        /// </summary>
        public string? SoneName { get; set; }

        /// <summary>
        /// 难度
        /// </summary>
        public Rank Rank { get; set; }

        /// <summary>
        /// 自定义难度文本
        /// </summary>
        public string? CustomRank { get; set; }

        /// <summary>
        /// 定数
        /// </summary>
        public double Level { get; set; }

        /// <summary>
        /// 曲师
        /// </summary>
        public string? Composer { get; set; }

        /// <summary>
        /// 谱师
        /// </summary>
        public string? Charter { get; set; }

        /// <summary>
        /// 画师
        /// </summary>
        public string? Illustrator { get; set; }

        /// <summary>
        /// BPM 属性
        /// </summary>
        public BpmProperty Bpm { get; }

        /// <summary>
        /// 判定线
        /// </summary>
        public RelationObject<Chart>.Collection<JudgementLine> JudgementLines { get; }

        /// <summary>
        /// 插值方向
        /// </summary>
        public KeyFrameEasingDirection KeyFrameEasingDirection { get; set; }

        public Chart()
        {
            Bpm = new BpmProperty(this);
            JudgementLines = new RelationObject<Chart>.Collection<JudgementLine>(this);
        }
    }
}
