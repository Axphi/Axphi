using System.Windows;
using Axphi.Data;
using Axphi.Data.Abstraction;
using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;

namespace Axphi.Utilities
{
    internal static class EasingUtils
    {
        private static void SelectTransitionKeyFrames<T>(
            TimeSpan time, IReadOnlyList<IKeyFrame<T>> keyFrames,
            out IKeyFrame<T>? firstKeyFrame, out IKeyFrame<T>? secondKeyFrame, out double normalizedTime)
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
            IKeyFrame<T>? firstKeyFrame, IKeyFrame<T>? secondKeyFrame,
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
            T initialValue, IReadOnlyList<IKeyFrame<T>> keyFrames,
            Func<T, T, double, T> lerpFunction,
            out T finalValue)
            where T : struct
        {
            finalValue = initialValue;

            SelectTransitionKeyFrames(time, keyFrames, out var firstKeyFrame, out var secondKeyFrame, out var t);
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
            IWithStandardAnimatableProperties properties,
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
