using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axphi.Data.KeyFrames
{
    /// <summary>
    /// 表示 <see cref="KeyFrameBase.Easing"/> 属性表示是与前一个关键帧的插值方式, 还是与后一个关键帧的插值方式
    /// </summary>
    public enum KeyFrameEasingDirection
    {
        /// <summary>
        /// 传入插值
        /// </summary>
        FromLast,

        /// <summary>
        /// 传出插值
        /// </summary>
        ToNext
    }
}
