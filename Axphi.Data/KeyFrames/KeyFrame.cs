using Axphi.Data.Abstraction;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.Data.KeyFrames
{
    public partial class KeyFrame<TParent, T> : RelationObject<TParent>, IKeyFrame<T>
        where TParent : class
        where T : struct
    {
        /// <summary>
        /// 时间
        /// </summary>
        [ObservableProperty]
        private TimeSpan _time;

        /// <summary>
        /// 从上一个 BPM 关键帧之间的插值方式
        /// </summary>
        [ObservableProperty]
        private BezierEasing? _easing = BezierEasing.Linear;

        /// <summary>
        /// 关键帧值
        /// </summary>
        [ObservableProperty]
        private T _value;
    }

}
