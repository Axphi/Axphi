namespace Axphi.Data.KeyFrames
{


    // 提取一个非泛型接口，只暴露时间轴 UI 关心的属性
    public interface IKeyFrame
    {
        int Tick { get; set; }
        // 如果你以后有缓动曲线类型（Easing），也可以加在这里，因为它也不依赖泛型 T
    }


    public record class KeyFrame<T>: IKeyFrame
        where T : struct
    {
        /// <summary>
        /// 时间
        /// </summary>
        public int Tick { get; set; }

        /// <summary>
        /// 从此关键帧到下一个关键帧之间的插值方式
        /// </summary>
        public BezierEasing Easing { get; set; } = Axphi.Utilities.BezierPresets.Linear;

        /// <summary>
        /// 是否为定格关键帧。为 true 时，该关键帧之后到下一个关键帧前保持常量值。
        /// </summary>
        public bool IsFreezeKeyframe { get; set; }

        /// <summary>
        /// 关键帧值
        /// </summary>
        public T Value { get; set; }
    }

}
