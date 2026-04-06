using Axphi.Data.KeyFrames;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("formatVersion")]
        public string FormatVersion { get; set; } = "1.0";

        [JsonIgnore]
        public string formatVersion
        {
            get => FormatVersion;
            set => FormatVersion = value;
        }

        // 关于时间的单位(int), 为 tick, 一个 tick 表示一个 128 分音符, 也就是 1.875 / bpm

        /// <summary>
        /// 时长
        /// </summary>
        public int Duration { get; set; } = 0;

        /// <summary>
        /// 歌曲名称
        /// </summary>
        [JsonPropertyName("SoneName")]
        public string SongName { get; set; } = string.Empty;

        [JsonIgnore]
        public string SoneName
        {
            get => SongName;
            set => SongName = value;
        }

        /// <summary>
        /// 难度
        /// </summary>
        public Rank Rank { get; set; } = new Rank();

        /// <summary>
        /// 自定义难度文本
        /// </summary>
        public string? CustomRank { get; set; }

        /// <summary>
        /// 定数
        /// </summary>
        public double Level { get; set; } = 0.0;

        /// <summary>
        /// 曲师
        /// </summary>
        public string Composer { get; set; } = string.Empty;

        /// <summary>
        /// 谱师
        /// </summary>
        public string Charter { get; set; } = string.Empty;

        /// <summary>
        /// 画师
        /// </summary>
        public string Illustrator { get; set; } = string.Empty;

        /// <summary>
        /// 全局初始 BPM（当没有任何 BPM 关键帧时使用）
        /// </summary>
        public double InitialBpm { get; set; } = 120.0;

        /// <summary>
        /// BPM 关键帧
        /// </summary>
        public List<KeyFrame<double>> BpmKeyFrames { get; set; } = new();

        /// <summary>
        /// 判定线
        /// </summary>
        public List<JudgementLine> JudgementLines { get; set; } = new();

        /// <summary>
        /// 插值方向
        /// </summary>
        public KeyFrameEasingDirection KeyFrameEasingDirection { get; set; } = KeyFrameEasingDirection.ToNext;

        public void RebuildHierarchy()
        {
            var lineById = new Dictionary<string, JudgementLine>(StringComparer.Ordinal);

            foreach (var line in JudgementLines)
            {
                line.ParentChart = this;
                line.ParentLine = null;

                if (!string.IsNullOrWhiteSpace(line.ID) && !lineById.ContainsKey(line.ID))
                {
                    lineById[line.ID] = line;
                }

                foreach (var note in line.Notes)
                {
                    note.ParentLine = line;
                }
            }

            foreach (var line in JudgementLines)
            {
                if (!string.IsNullOrWhiteSpace(line.ParentLineId) && lineById.TryGetValue(line.ParentLineId, out var parentLine))
                {
                    line.ParentLine = parentLine;
                }
            }
        }

    }
}
