using Axphi.Data.KeyFrames;
using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Axphi.Utilities
{
    internal static class EasingUtils
    {
        private static void SelectTransitionKeyFrames<T>(
            TimeSpan time, IReadOnlyList<KeyFrame<T>> keyFrames,
            out KeyFrame<T>? firstKeyFrame, out KeyFrame<T>? secondKeyFrame, out double normalizedTime)
            where T : struct
        {
            firstKeyFrame = null;
            secondKeyFrame = null;
            normalizedTime = 0;

            var lastTime = default(TimeSpan);
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
                    normalizedTime = elapsed / total;
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
            y = easing.HasValue ? easing.Value.Calculate(t) : 0;
        }

        public static void CalculateObjectSingleTransform<T>(
            TimeSpan time,
            KeyFrameEasingDirection easingDirection,
            T initialValue, IReadOnlyList<KeyFrame<T>> keyFrames,
            Func<T, T, double, T> lerpFunction,
            out T finalValue)
            where T : struct
        {
            finalValue = initialValue;

            SelectTransitionKeyFrames<T>(time, keyFrames, out var firstKeyFrame, out var secondKeyFrame, out var t);
            SelectKeyFrameEasing(easingDirection, firstKeyFrame, secondKeyFrame, out var easing);

            var start = firstKeyFrame?.Value ?? initialValue;
            if (secondKeyFrame is not null)
            {
                CalculateEasingY(easing, t, out var y);
                finalValue = lerpFunction.Invoke(start, secondKeyFrame.Value, y);
            }
        }

        public static void CalculateObjectTransform(
            TimeSpan time,
            KeyFrameEasingDirection easingDirection,
            Vector initialOffset, Vector initialScale, double initialRotationAngle, double initialOpacity,
            TransformKeyFrames? transformKeyFrames,
            out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            finalOffset = initialOffset;
            finalScale = initialScale;
            finalRotationAngle = initialRotationAngle;
            finalOpacity = initialOpacity;

            if (transformKeyFrames is null)
            {
                return;
            }

            if (transformKeyFrames.OffsetKeyFrames is { } offsetKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, initialOffset, offsetKeyFrames, MathUtils.Lerp, out finalOffset);
            }

            if (transformKeyFrames.ScaleKeyFrames is { } scaleKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, initialScale, scaleKeyFrames, MathUtils.Lerp, out finalScale);
            }

            if (transformKeyFrames.RotationKeyFrames is { } rotationKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, initialRotationAngle, rotationKeyFrames, MathUtils.Lerp, out finalRotationAngle);
            }

            if (transformKeyFrames.OpacityKeyFrames is { } opacityKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, initialOpacity, opacityKeyFrames, MathUtils.Lerp, out finalOpacity);
            }
        }

    }
}
