using Axphi.Data.KeyFrames;
using System.Collections.Generic;

namespace Axphi.Utilities
{
    /// <summary>
    /// 专门负责处理关键帧数据的工具类（负责找数据、处理幽灵帧，不负责复杂的数学插值）
    /// </summary>
    public static class KeyFrameUtils
    {
        /// <summary>
        /// 获取在指定 Tick 的关键帧值（阶跃/突变模式，不插值）
        /// 完美包含幽灵关键帧（边界外钳位）逻辑！
        /// 适用于：BPM、布尔值开关、任何不需要平滑过渡的离散属性。
        /// </summary>
        /// <typeparam name="T">关键帧存储的数据类型</typeparam>
        /// <param name="keyFrames">底层的关键帧集合（必须是已排序的）</param>
        /// <param name="currentTick">当前要查询的时间</param>
        /// <param name="defaultValue">如果没有关键帧时，系统应该脑补的“幽灵帧”默认值</param>
        /// <returns>计算出的当前值</returns>
        public static T GetStepValueAtTick<T>(IReadOnlyList<KeyFrame<T>> keyFrames, int currentTick, T defaultValue)
            where T : struct
        {
            // ================= 幽灵帧逻辑 1：完全没有任何关键帧 =================
            if (keyFrames == null || keyFrames.Count == 0)
            {
                return defaultValue;
            }

            // ================= 幽灵帧逻辑 2：当前时间早于第一个关键帧 =================
            var firstFrame = keyFrames[0];
            if (currentTick <= firstFrame.Tick)
            {
                return firstFrame.Value;
            }

            // ================= 幽灵帧逻辑 3：当前时间晚于最后一个关键帧 =================
            var lastFrame = keyFrames[keyFrames.Count - 1];
            if (currentTick >= lastFrame.Tick)
            {
                return lastFrame.Value;
            }

            // ================= 正常逻辑：在两个关键帧之间（Step 模式） =================
            // 遍历寻找当前时间所在的区间
            for (int i = 0; i < keyFrames.Count - 1; i++)
            {
                var frameA = keyFrames[i];
                var frameB = keyFrames[i + 1];

                if (currentTick >= frameA.Tick && currentTick < frameB.Tick)
                {
                    // 因为是 Step 模式，所以不到下一个帧的时间，就永远保持当前帧的值！
                    return frameA.Value;
                }
            }

            // 兜底（理论上代码不会走到这里）
            return defaultValue;
        }
    }
}