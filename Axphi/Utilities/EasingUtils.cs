using System.Windows;
using Axphi.Data;
using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;

namespace Axphi.Utilities
{
    internal static class EasingUtils
    {
        private static void SelectTransitionKeyFrames<TParent, T>(
            TimeSpan time, IReadOnlyList<KeyFrame<TParent, T>> keyFrames,
            out KeyFrame<TParent, T>? firstKeyFrame, out KeyFrame<TParent, T>? secondKeyFrame, out double normalizedTime)
            where TParent : class
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

        private static void SelectKeyFrameEasing<TParent, T>(
            KeyFrameEasingDirection easingDirection,
            KeyFrame<TParent, T>? firstKeyFrame, KeyFrame<TParent, T>? secondKeyFrame,
            out BezierEasing? easing)
            where TParent : class
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

        public static void CalculateObjectSingleTransform<TParent, T>(
            TimeSpan time,
            KeyFrameEasingDirection easingDirection,
            T initialValue, IReadOnlyList<KeyFrame<TParent, T>> keyFrames,
            Func<T, T, double, T> lerpFunction,
            out T finalValue)
            where TParent : class
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

        public static void CalculateObjectTransform<T>(
            TimeSpan time,
            KeyFrameEasingDirection easingDirection,
            StandardAnimatableProperties<T> properties,
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
