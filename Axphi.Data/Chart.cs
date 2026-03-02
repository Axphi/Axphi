using Axphi.Data.KeyFrames;
using System.Collections.ObjectModel; // 别忘了引入这个命名空间
using static System.Net.Mime.MediaTypeNames;

namespace Axphi.Data
{
    /// <summary>
    /// 谱面
    /// </summary>
    public class Chart
    {
        /// <summary>
        /// 谱面版本
        /// </summary>
        public string formatVersion = "1.0";

        // 关于时间的单位(int), 为 tick, 一个 tick 表示一个 128 分音符, 也就是 1.875 / bpm

        /// <summary>
        /// 音频偏移
        /// </summary>  
        public int Offset { get; set; }

        /// <summary>
        /// 时长
        /// </summary>
        public int Duration { get; set; }

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
        /// BPM 关键帧
        /// </summary>
        public List<KeyFrame<double>>? BpmKeyFrames { get; set; }

        /// <summary>
        /// 判定线
        /// </summary>
        /// // 新代码：全面换装 ObservableCollection 大喇叭！
        public ObservableCollection<JudgementLine>? JudgementLines { get; set; }

        /// <summary>
        /// 插值方向
        /// </summary>
        public KeyFrameEasingDirection KeyFrameEasingDirection { get; set; } = KeyFrameEasingDirection.ToNext;
    }
}
