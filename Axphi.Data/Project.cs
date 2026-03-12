using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axphi.Data
{
    public class Project
    {
        /// <summary>
        /// 谱面
        /// </summary>
        public Chart Chart { get; set; } = new Chart();

        /// <summary>
        /// 音频
        /// </summary>
        public byte[]? EncodedAudio { get; set; }

        /// <summary>
        /// 曲绘
        /// </summary>
        public byte[]? EncodedIllustration { get; set; }
    }
}
