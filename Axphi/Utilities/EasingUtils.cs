using Axphi.Data.KeyFrames;
using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Windows;
using Axphi.Data.AnimatableProperties;

namespace Axphi.Utilities
{
    internal static class EasingUtils
    {
        private static void SelectTransitionKeyFrames<T>(
            double time, IReadOnlyList<KeyFrame<T>> keyFrames,
            out KeyFrame<T>? firstKeyFrame, out KeyFrame<T>? secondKeyFrame, out double normalizedTime)
            where T : struct
        {
            firstKeyFrame = null;
            secondKeyFrame = null;
            normalizedTime = 0;

            int left = 0;
            int right = keyFrames.Count - 1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (keyFrames[mid].Tick <= time)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            if (left > 0)
            {
                firstKeyFrame = keyFrames[left - 1];
            }

            if (left < keyFrames.Count)
            {
                secondKeyFrame = keyFrames[left];

                double lastTime = left > 0 ? keyFrames[left - 1].Tick : 0;

                double elapsed = time - lastTime;
                double total = keyFrames[left].Tick - lastTime;

                normalizedTime = total > 0 ? (double)elapsed / total : 1.0;
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
            CalculateObjectSingleTransform((double)time, easingDirection, initialValue, keyFrames, lerpFunction, out finalValue);
        }

        public static void CalculateObjectSingleTransform<T>(
            double time,
            KeyFrameEasingDirection easingDirection,
            T initialValue, IReadOnlyList<KeyFrame<T>> keyFrames,
            Func<T, T, double, T> lerpFunction,
            out T finalValue)
            where T : struct
        {
            if (keyFrames.Count == 0)
            {
                finalValue = initialValue;
                return;
            }

            var firstFrame = keyFrames[0];
            if (time <= firstFrame.Tick)
            {
                finalValue = firstFrame.Value;
                return;
            }

            var lastFrame = keyFrames[keyFrames.Count - 1];
            if (time >= lastFrame.Tick)
            {
                finalValue = lastFrame.Value;
                return;
            }

            SelectTransitionKeyFrames<T>(time, keyFrames, out var firstKeyFrame, out var secondKeyFrame, out var t);
            SelectKeyFrameEasing(easingDirection, firstKeyFrame, secondKeyFrame, out var easing);

            var start = firstKeyFrame?.Value ?? initialValue;

            if (firstKeyFrame?.IsFreezeKeyframe == true)
            {
                finalValue = start;
                return;
            }

            finalValue = start;

            if (secondKeyFrame is not null)
            {
                CalculateEasingY(easing, t, out var y);
                finalValue = lerpFunction.Invoke(start, secondKeyFrame.Value, y);
            }
        }

        public static void CalculateObjectSingleTransform(
            double time,
            KeyFrameEasingDirection easingDirection,
            double initialValue,
            IReadOnlyList<KeyFrame<double>> keyFrames,
            Func<double, double, double, double> lerpFunction,
            bool expressionEnabled,
            string? expressionText,
            Chart? chart,
            JudgementLine? currentLine,
            out double finalValue)
        {
            CalculateObjectSingleTransform(time, easingDirection, initialValue, keyFrames, lerpFunction, out finalValue);

            // TODO: Expression 逻辑暂留
            if (!expressionEnabled || (currentLine != null /*&& !currentLine.SpeedExpressionIsValid)*/))
            {
                return;
            }
        }

        public static void CalculateObjectSingleTransform(
            int time,
            KeyFrameEasingDirection easingDirection,
            double initialValue,
            IReadOnlyList<KeyFrame<double>> keyFrames,
            Func<double, double, double, double> lerpFunction,
            bool expressionEnabled,
            string? expressionText,
            Chart? chart,
            JudgementLine? currentLine,
            out double finalValue)
        {
            CalculateObjectSingleTransform((double)time, easingDirection, initialValue, keyFrames, lerpFunction, expressionEnabled, expressionText, chart, currentLine, out finalValue);
        }

        public static void CalculateObjectSingleTransform(
            double time,
            KeyFrameEasingDirection easingDirection,
            double initialValue,
            IReadOnlyList<KeyFrame<double>> keyFrames,
            Func<double, double, double, double> lerpFunction,
            bool expressionEnabled,
            string? expressionText,
            Chart? chart,
            out double finalValue)
        {
            CalculateObjectSingleTransform(time, easingDirection, initialValue, keyFrames, lerpFunction, expressionEnabled, expressionText, chart, null, out finalValue);
        }

        public static void CalculateObjectSingleTransform(
            int time,
            KeyFrameEasingDirection easingDirection,
            double initialValue,
            IReadOnlyList<KeyFrame<double>> keyFrames,
            Func<double, double, double, double> lerpFunction,
            bool expressionEnabled,
            string? expressionText,
            Chart? chart,
            out double finalValue)
        {
            CalculateObjectSingleTransform((double)time, easingDirection, initialValue, keyFrames, lerpFunction, expressionEnabled, expressionText, chart, null, out finalValue);
        }


        // ================= 核心修改点：统一使用 ITransformProperties =================

        public static void CalculateObjectTransform(
            int time,
            KeyFrameEasingDirection easingDirection,
            IProperties properties, // 👈 修改
            out Vector finalAnchor, out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            CalculateObjectTransform((double)time, easingDirection, properties, null, out finalAnchor, out finalOffset, out finalScale, out finalRotationAngle, out finalOpacity);
        }

        public static void CalculateObjectTransform(
            double time,
            KeyFrameEasingDirection easingDirection,
            IProperties properties, // 👈 修改
            out Vector finalAnchor, out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            CalculateObjectTransform(time, easingDirection, properties, null, out finalAnchor, out finalOffset, out finalScale, out finalRotationAngle, out finalOpacity);
        }

        public static void CalculateObjectTransform(
            int time,
            KeyFrameEasingDirection easingDirection,
            IProperties properties, // 👈 修改
            Chart? chart,
            out Vector finalAnchor, out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            CalculateObjectTransform((double)time, easingDirection, properties, chart, out finalAnchor, out finalOffset, out finalScale, out finalRotationAngle, out finalOpacity);
        }

        public static void CalculateObjectTransform(
            double time,
            KeyFrameEasingDirection easingDirection,
            IProperties properties, // 👈 修改
            Chart? chart,
            JudgementLine? currentLine,
            out Vector finalAnchor, out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            finalAnchor = properties.Anchor.InitialValue;

            // 👈 修改：全部把 Offset 换成 Position
            finalOffset = properties.Position.InitialValue;

            finalScale = properties.Scale.InitialValue;
            finalRotationAngle = properties.Rotation.InitialValue;
            finalOpacity = properties.Opacity.InitialValue;


            if (properties.Anchor.KeyFrames is { } anchorKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, properties.Anchor.InitialValue, anchorKeyFrames, MathUtils.Lerp, out finalAnchor);
            }

            // 👈 修改：Offset 换成 Position
            if (properties.Position.KeyFrames is { } positionKeyFrames)
            {
                CalculateObjectSingleTransform(time, easingDirection, properties.Position.InitialValue, positionKeyFrames, MathUtils.Lerp, out finalOffset);
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

        public static void CalculateObjectTransform(
            double time,
            KeyFrameEasingDirection easingDirection,
            IProperties properties, // 👈 修改
            Chart? chart,
            out Vector finalAnchor, out Vector finalOffset, out Vector finalScale, out double finalRotationAngle, out double finalOpacity)
        {
            CalculateObjectTransform(time, easingDirection, properties, chart, null, out finalAnchor, out finalOffset, out finalScale, out finalRotationAngle, out finalOpacity);
        }
    }
}