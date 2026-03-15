using Axphi.Data.KeyFrames;
using System.Collections.Generic;

namespace Axphi.Utilities
{
    public static class TimeTickConverter
    {
        /// <summary>
        /// 物理时间 (Seconds) 转 逻辑时间 (Tick)
        /// </summary>
        public static double TimeToTick(double targetSeconds, IReadOnlyList<KeyFrame<double>> bpmKeyFrames, double defaultBpm = 120.0)
        {
            if (bpmKeyFrames == null || bpmKeyFrames.Count == 0)
                return targetSeconds / (1.875 / defaultBpm);

            double accumulatedSeconds = 0;
            double accumulatedTicks = 0;
            // 只要有关键帧，起步速度就是第一个关键帧的值！彻底抛弃 defaultBpm
            double currentBpm = bpmKeyFrames[0].Value;

            for (int i = 0; i < bpmKeyFrames.Count; i++)
            {
                var frame = bpmKeyFrames[i];
                if (frame.Time <= 0) continue; // Tick=0 已经被当做初始值了

                double ticksInSegment = frame.Time - accumulatedTicks;
                double secondsInSegment = ticksInSegment * (1.875 / currentBpm);

                // 如果目标时间在这个区间内，结束累加
                if (accumulatedSeconds + secondsInSegment >= targetSeconds)
                {
                    break;
                }

                accumulatedSeconds += secondsInSegment;
                accumulatedTicks = frame.Time;
                currentBpm = frame.Value;
            }

            // 加上最后一段零头的 Tick
            double remainingSeconds = targetSeconds - accumulatedSeconds;
            accumulatedTicks += remainingSeconds / (1.875 / currentBpm);

            return accumulatedTicks;
        }

        /// <summary>
        /// 逻辑时间 (Tick) 转 物理时间 (Seconds)
        /// </summary>
        public static double TickToTime(double targetTick, IReadOnlyList<KeyFrame<double>> bpmKeyFrames, double defaultBpm = 120.0)
        {
            if (bpmKeyFrames == null || bpmKeyFrames.Count == 0)
                return targetTick * (1.875 / defaultBpm);

            double accumulatedSeconds = 0;
            double accumulatedTicks = 0;
            // 核心修改：起步速度锁定为第一个关键帧的值
            double currentBpm = bpmKeyFrames[0].Value;

            for (int i = 0; i < bpmKeyFrames.Count; i++)
            {
                var frame = bpmKeyFrames[i];
                if (frame.Time <= 0) continue;

                if (frame.Time >= targetTick)
                    break;

                double ticksInSegment = frame.Time - accumulatedTicks;
                accumulatedSeconds += ticksInSegment * (1.875 / currentBpm);
                accumulatedTicks = frame.Time;
                currentBpm = frame.Value;
            }

            // 加上最后一段零头的时间
            double remainingTicks = targetTick - accumulatedTicks;
            accumulatedSeconds += remainingTicks * (1.875 / currentBpm);

            return accumulatedSeconds;
        }
    }
}