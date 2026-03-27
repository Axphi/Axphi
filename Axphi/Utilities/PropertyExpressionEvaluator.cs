using Axphi.Data;
using Jint;
using Jint.Native;
using Jint.Runtime;
using System;
using System.Windows;

namespace Axphi.Utilities
{
    public readonly record struct ExpressionRuntimeContext(double Tick, double Time, double Bpm);

    public static class PropertyExpressionEvaluator
    {
        public static ExpressionRuntimeContext CreateContext(double tick, Chart? chart)
        {
            if (chart == null)
            {
                return new ExpressionRuntimeContext(tick, tick, 120.0);
            }

            double seconds = TimeTickConverter.TickToTime(tick, chart.BpmKeyFrames, chart.InitialBpm);
            double bpm = ResolveBpmAtTick(tick, chart);
            return new ExpressionRuntimeContext(tick, seconds, bpm);
        }

        public static ExpressionRuntimeContext CreateDesignTimeContext() => new(0, 0, 120.0);

        public static bool TryEvaluateDouble(string? expression, double baseValue, ExpressionRuntimeContext context, out double value, out string? error)
        {
            value = baseValue;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                var result = Evaluate(expression, context, baseValue);
                if (!TryConvertToDouble(result, out var convertedValue))
                {
                    error = "表达式结果必须是数字";
                    return false;
                }

                value = convertedValue;
                return true;
            }
            catch (JavaScriptException exception)
            {
                error = exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryEvaluateVector(string? expression, Vector baseValue, ExpressionRuntimeContext context, out Vector value, out string? error)
        {
            value = baseValue;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                var result = Evaluate(expression, context, new[] { baseValue.X, baseValue.Y });
                if (!TryConvertToVector(result, out var convertedValue))
                {
                    error = "二维属性表达式必须返回 [x, y]";
                    return false;
                }

                value = convertedValue;
                return true;
            }
            catch (JavaScriptException exception)
            {
                error = exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static JsValue Evaluate(string expression, ExpressionRuntimeContext context, object baseValue)
        {
            string normalizedExpression = NormalizeExpression(expression);
            var engine = new Engine(options => options.LimitRecursion(64).MaxStatements(512));
            engine.SetValue("tick", context.Tick);
            engine.SetValue("time", context.Time);
            engine.SetValue("bpm", context.Bpm);
            engine.SetValue("value", baseValue);

            return engine.Evaluate($"(() => {{ {normalizedExpression} }})()");
        }

        private static string NormalizeExpression(string expression)
        {
            string trimmed = expression.Trim();
            if (trimmed.Contains("return", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return $"return ({trimmed});";
        }

        private static bool TryConvertToDouble(JsValue result, out double value)
        {
            value = 0;
            if (!result.IsNumber())
            {
                return false;
            }

            value = result.AsNumber();
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryConvertToVector(JsValue result, out Vector value)
        {
            value = default;

            if (result.IsArray())
            {
                var array = result.AsArray();
                if (!TryReadArrayItem(array.Get(0), out double x) || !TryReadArrayItem(array.Get(1), out double y))
                {
                    return false;
                }

                value = new Vector(x, y);
                return true;
            }

            if (result.IsObject())
            {
                var obj = result.AsObject();
                if (!TryReadArrayItem(obj.Get("x"), out double x) || !TryReadArrayItem(obj.Get("y"), out double y))
                {
                    return false;
                }

                value = new Vector(x, y);
                return true;
            }

            return false;
        }

        private static bool TryReadArrayItem(JsValue value, out double number)
        {
            number = 0;
            if (!value.IsNumber())
            {
                return false;
            }

            number = value.AsNumber();
            return !double.IsNaN(number) && !double.IsInfinity(number);
        }

        private static double ResolveBpmAtTick(double tick, Chart chart)
        {
            double resolvedBpm = chart.InitialBpm > 0 ? chart.InitialBpm : 120.0;
            if (chart.BpmKeyFrames == null || chart.BpmKeyFrames.Count == 0)
            {
                return resolvedBpm;
            }

            foreach (var keyFrame in chart.BpmKeyFrames)
            {
                if (keyFrame.Time > tick)
                {
                    break;
                }

                resolvedBpm = keyFrame.Value;
            }

            return resolvedBpm;
        }
    }
}