using System.Collections.ObjectModel;

namespace Axphi.Data.KeyFrames
{
    public class TransformKeyFrames
    {
        /// <summary>
        /// 偏移
        /// </summary>
        public List<VectorKeyFrame>? OffsetKeyFrames { get; set; }

        /// <summary>
        /// 缩放
        /// </summary>
        public List<ScaleKeyFrame>? ScaleKeyFrames { get; set; }

        /// <summary>
        /// 旋转
        /// </summary>
        public List<RotationKeyFrame>? RotationKeyFrames { get; set; }


        /// <summary>
        /// 不透明度
        /// </summary>
        public List<OpacityKeyFrame>? OpacityKeyFrames { get; set; }
    }
}
