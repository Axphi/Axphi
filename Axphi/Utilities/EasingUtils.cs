using Axphi.Data.KeyFrames;
using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Axphi.Data.AnimatableProperties;

namespace Axphi.Utilities
{
    internal static class EasingUtils
    {
        private static void SelectTransitionKeyFrames<T>(
            int time, IReadOnlyList<KeyFrame<T>> keyFrames,
            out KeyFrame<T>? firstKeyFrame, out KeyFrame<T>? secondKeyFrame, out double normalizedTime)
            where T : struct
        {
            firstKeyFrame = null;
            secondKeyFrame = null;
            normalizedTime = 0;

            //var lastTime = default(TimeSpan);
            // 默认值从 default(TimeSpan) 改为 0
            var lastTime = 0;

            foreach (var keyFrame in keyFrames)
            {
                if (keyFrame.Time <= time)
                {
                    firstKeyFrame = keyFrame;
                    lastTime = keyFrame.Time;
                }
                else
                {

                    secondKeyFrame = keyFrame;
                    var elapsed = time - lastTime;
                    var total = keyFrame.Time - lastTime;

                    // [修改 4 - 重要！] 必须将 int 转为 double 再除，否则 50 / 100 会变成 0！
                    // 同时加入 total > 0 的判断，防止同一时间点有两个关键帧导致“除以 0”崩溃
                    normalizedTime = total > 0 ? (double)elapsed / total : 1.0;
                    //normalizedTime = elapsed / total;

                    return;
                }
            }
        }

        private static void SelectKeyFrameEasing<T>(
            KeyFrameEasingDirection easingDirection,
            KeyFrame<T>? firstKeyFrame, KeyFrame<T>? secondKeyFrame,
            out BezierEasing? easing)
            where T : struct
        {
            easing = easingDirection switch
            {
                KeyFrameEasingDirection.FromLast => secondKeyFrame?.Easing,
                KeyFrameEasingDirection.ToNext => firstKeyFrame?.Easing,
                _ => null
            };
        }

        private static void CalculateEasingY(
            BezierEasing? easing, double t, out double y)
        {
            y = easing.HasValue ? BezierCalculator.Calculate(easing.Value, t) : t;
        }

        public static void CalculateObjectSingleTransform<T>(
            int time,
            KeyFrameEasingDirection easingDirection,
            T initialValue, IReadOnlyList<KeyFrame<T>> keyFrames,
            Func<T, T, double, T> lerpFunction,
            out T finalValue)
            where T : struct
        {



            // ================= 幽灵帧逻辑 1：完全没有任何关键帧 =================
            // 效果：判定线老老实实呆在原点 (InitialValue)，UI 不显示任何菱形。
            if (keyFrames.Count == 0)
            {
                finalValue = initialValue;
                return;
            }

            // ================= 幽灵帧逻辑 2：当前时间早于或等于第一个关键帧 =================
            // 效果：如果你只在 Tick=500 打了关键帧，那么从 Tick=0 到 500 的时间里，
            // 值永远等于 Tick=500 时的值，绝对不会产生诡异的倒退动画！
            var firstFrame = keyFrames[0];
            if (time <= firstFrame.Time)
            {
                finalValue = firstFrame.Value;
                return;
            }

            // ================= 幽灵帧逻辑 3：当前时间晚于或等于最后一个关键帧 =================
            // 效果：越界保护。动画播完最后一个关键帧后，直接锁死在最后的状态。
            var lastFrame = keyFrames[keyFrames.Count - 1];
            if (time >= lastFrame.Time)
            {
                finalValue = lastFrame.Value;
                return;
            }


            SelectTransitionKeyFrames<T>(time, keyFrames, out var firstKeyFrame, out var secondKeyFrame, out var t);
            SelectKeyFrameEasing(easingDirection, firstKeyFrame, secondKeyFrame, out var easing);

            var start = firstKeyFrame?.Value ?? initialValue;

            // 【核心修复】先把 finalValue 稳稳地定在这个基础值上！
            // 这样即使后面是 null 进不去 if，它也会保持最后一个帧的状态，而不是回弹！
            finalValue = start;

            if (secondKeyFrame is not null)
            {
                CalculateEasingY(easing, t, out var y);
                finalValue = lerpFunction.Invoke(start, secondKeyFrame.Value, y);

            }
        }

        
        public static void CalculateObjectTransform(
            int time,
            KeyFrameEasingDirection easingDirection,
            StandardAnimatableProperties properties,
            out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            finalOffset = properties.Offset.InitialValue;
            finalScale = properties.Scale.InitialValue;
            finalRotationAngle = properties.Rotation.InitialValue;
            finalOpacity = properties.Opacity.InitialValue;

            if (properties.Offset.KeyFrames is { } offsetKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, properties.Offset.InitialValue, offsetKeyFrames, MathUtils.Lerp, out finalOffset);
            }

            if (properties.Scale.KeyFrames is { } scaleKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, properties.Scale.InitialValue, scaleKeyFrames, MathUtils.Lerp, out finalScale);
            }

            if (properties.Rotation.KeyFrames is { } rotationKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, properties.Rotation.InitialValue, rotationKeyFrames, MathUtils.Lerp, out finalRotationAngle);
            }

            if (properties.Opacity.KeyFrames is { } opacityKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, properties.Opacity.InitialValue, opacityKeyFrames, MathUtils.Lerp, out finalOpacity);
            }
        }


    }
}
