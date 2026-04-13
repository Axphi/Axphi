using Axphi.Data;
using Axphi.Data.KeyFrames;
using Jint;
using Jint.Native;
using Jint.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Axphi.Utilities
{
    public readonly record struct ExpressionRuntimeContext(double Tick, double Time, double Bpm);

    public static class PropertyExpressionEvaluator
    {
        private static readonly AsyncLocal<HashSet<string>?> AmbientEvaluationStack = new();

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
            return TryEvaluateDouble(expression, baseValue, context, null, null, null, null, out value, out error);
        }

        public static bool TryEvaluateDouble(
            string? expression,
            double baseValue,
            ExpressionRuntimeContext context,
            Chart? chart,
            JudgementLine? currentLine,
            out double value,
            out string? error)
        {
            return TryEvaluateDouble(expression, baseValue, context, chart, currentLine, null, null, out value, out error);
        }

        internal static bool TryEvaluateDouble(
            string? expression,
            double baseValue,
            ExpressionRuntimeContext context,
            Chart? chart,
            JudgementLine? currentLine,
            string? propertyName,
            HashSet<string>? evaluationStack,
            out double value,
            out string? error)
        {
            value = baseValue;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                var result = Evaluate(expression, context, baseValue, expectsVector: false, chart, currentLine, propertyName, evaluationStack);
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
            return TryEvaluateVector(expression, baseValue, context, null, null, null, null, out value, out error);
        }

        public static bool TryEvaluateVector(
            string? expression,
            Vector baseValue,
            ExpressionRuntimeContext context,
            Chart? chart,
            JudgementLine? currentLine,
            out Vector value,
            out string? error)
        {
            return TryEvaluateVector(expression, baseValue, context, chart, currentLine, null, null, out value, out error);
        }

        internal static bool TryEvaluateVector(
            string? expression,
            Vector baseValue,
            ExpressionRuntimeContext context,
            Chart? chart,
            JudgementLine? currentLine,
            string? propertyName,
            HashSet<string>? evaluationStack,
            out Vector value,
            out string? error)
        {
            value = baseValue;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                var result = Evaluate(expression, context, new[] { baseValue.X, baseValue.Y }, expectsVector: true, chart, currentLine, propertyName, evaluationStack);
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

        private static JsValue Evaluate(
            string expression,
            ExpressionRuntimeContext context,
            object baseValue,
            bool expectsVector,
            Chart? chart,
            JudgementLine? currentLine,
            string? propertyName,
            HashSet<string>? evaluationStack)
        {
            evaluationStack ??= AmbientEvaluationStack.Value ?? new HashSet<string>(StringComparer.Ordinal);
            string? currentFrameKey = null;
            HashSet<string>? previousStack = AmbientEvaluationStack.Value;
            if (currentLine != null && !string.IsNullOrWhiteSpace(propertyName))
            {
                currentFrameKey = CreateEvaluationKey(currentLine, propertyName!);
                if (!evaluationStack.Add(currentFrameKey))
                {
                    throw new InvalidOperationException($"检测到表达式循环引用: {currentLine.Name}.{propertyName}");
                }
            }

            string normalizedExpression = NormalizeExpression(expression, expectsVector);
            var engine = new Engine(options => options.LimitRecursion(64).MaxStatements(512));
            try
            {
                AmbientEvaluationStack.Value = evaluationStack;
                engine.SetValue("tick", context.Tick);
                engine.SetValue("time", context.Time);
                engine.SetValue("bpm", context.Bpm);
                engine.SetValue("value", baseValue);
                engine.SetValue("vec", (Func<double, double, double[]>)ExpressionMathHelpers.CreateVector);
                engine.SetValue("__ax_vecAdd", (Func<object?, object?, double[]>)ExpressionMathHelpers.VectorAdd);
                engine.SetValue("__ax_vecSub", (Func<object?, object?, double[]>)ExpressionMathHelpers.VectorSubtract);
                engine.SetValue("add", (Func<object?, object?, double[]>)ExpressionMathHelpers.VectorAdd);
                engine.SetValue("sub", (Func<object?, object?, double[]>)ExpressionMathHelpers.VectorSubtract);
                engine.SetValue("mul", (Func<object?, object?, object?>)ExpressionMathHelpers.Multiply);
                engine.SetValue("clamp", (Func<double, double, double, double>)Math.Clamp);
                engine.SetValue("lerp", (Func<double, double, double, double>)MathUtils.Lerp);

                if (chart != null)
                {
                    BindLineReferences(engine, chart, currentLine, context, evaluationStack);
                }

                return engine.Evaluate($"(() => {{ {normalizedExpression} }})()");
            }
            finally
            {
                AmbientEvaluationStack.Value = previousStack;
                if (currentFrameKey != null)
                {
                    evaluationStack.Remove(currentFrameKey);
                }
            }
        }

        private static string NormalizeExpression(string expression, bool expectsVector)
        {
            string trimmed = expression.Trim().TrimEnd(';').Trim();
            if (trimmed.Contains("return", StringComparison.Ordinal))
            {
                return trimmed;
            }

            int splitIndex = FindLastTopLevelStatementSeparator(trimmed);
            string prefix = splitIndex >= 0 ? trimmed[..(splitIndex + 1)] : string.Empty;
            string tail = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : trimmed;
            if (expectsVector)
            {
                tail = RewriteVectorSugar(tail);
            }

            return string.IsNullOrWhiteSpace(prefix)
                ? $"return ({tail});"
                : $"{prefix} return ({tail});";
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

        private static void BindLineReferences(Engine engine, Chart chart, JudgementLine? currentLine, ExpressionRuntimeContext context, HashSet<string> evaluationStack)
        {
            var lineLookup = new Dictionary<string, ExpressionLineProxy>(StringComparer.Ordinal);

            foreach (JudgementLine line in chart.JudgementLines)
            {
                var proxy = new ExpressionLineProxy(
                    line,
                    propertyName => ResolveLineProperty(line, propertyName, context, chart, evaluationStack),
                    noteKey => ResolveLineNote(line, noteKey, context, chart, evaluationStack));
                RegisterLineKey(lineLookup, line.ID, proxy);
                RegisterLineKey(lineLookup, line.Name, proxy);
            }

            if (currentLine != null)
            {
                engine.SetValue("self", lineLookup.TryGetValue(currentLine.ID, out var selfProxy)
                    ? selfProxy
                    : new ExpressionLineProxy(
                        currentLine,
                        propertyName => ResolveLineProperty(currentLine, propertyName, context, chart, evaluationStack),
                        noteKey => ResolveLineNote(currentLine, noteKey, context, chart, evaluationStack)));

                if (!string.IsNullOrWhiteSpace(currentLine.ParentLineId) && lineLookup.TryGetValue(currentLine.ParentLineId, out var parentProxy))
                {
                    engine.SetValue("parent", parentProxy);
                }
                else
                {
                    engine.SetValue("parent", JsValue.Null);
                }
            }

            engine.SetValue("line", (Func<string, object?>)(name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                lineLookup.TryGetValue(name, out var proxy);
                return proxy;
            }));
        }

        private static void RegisterLineKey(Dictionary<string, ExpressionLineProxy> lookup, string? key, ExpressionLineProxy proxy)
        {
            if (string.IsNullOrWhiteSpace(key) || lookup.ContainsKey(key))
            {
                return;
            }

            lookup[key] = proxy;
        }

        private static object ResolveLineProperty(JudgementLine line, string propertyName, ExpressionRuntimeContext context, Chart chart, HashSet<string> evaluationStack)
        {
            switch (propertyName)
            {
                case "anchor":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, line.AnimatableProperties, chart, line, out var anchor, out _, out _, out _, out _);
                    return new[] { anchor.X, anchor.Y };
                case "position":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, line.AnimatableProperties, chart, line, out _, out var position, out _, out _, out _);
                    return new[] { position.X, position.Y };
                case "scale":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, line.AnimatableProperties, chart, line, out _, out _, out var scale, out _, out _);
                    return new[] { scale.X, scale.Y };
                case "rotation":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, line.AnimatableProperties, chart, line, out _, out _, out _, out var rotation, out _);
                    return rotation;
                case "opacity":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, line.AnimatableProperties, chart, line, out _, out _, out _, out _, out var opacity);
                    return opacity;
                case "speed":
                    EasingUtils.CalculateObjectSingleTransform(context.Tick, chart.KeyFrameEasingDirection, line.InitialSpeed, line.SpeedKeyFrames, MathUtils.Lerp, line.SpeedExpressionEnabled, line.SpeedExpressionText, chart, line, out var speed);
                    return speed;
                case "tick":
                    return context.Tick;
                case "time":
                    return context.Time;
                case "bpm":
                    return context.Bpm;
                default:
                    return JsValue.Undefined;
            }
        }

        private static object? ResolveLineNote(JudgementLine line, string noteKey, ExpressionRuntimeContext context, Chart chart, HashSet<string> evaluationStack)
        {
            if (string.IsNullOrWhiteSpace(noteKey) || line.Notes == null)
            {
                return null;
            }

            Note? note = line.Notes.FirstOrDefault(candidate => string.Equals(candidate.ID, noteKey, StringComparison.Ordinal))
                ?? line.Notes.FirstOrDefault(candidate => string.Equals(candidate.Name, noteKey, StringComparison.Ordinal));

            if (note == null)
            {
                return null;
            }

            return new ExpressionNoteProxy(note, propertyName => ResolveNoteProperty(note, propertyName, context, chart, evaluationStack));
        }

        private static object ResolveNoteProperty(Note note, string propertyName, ExpressionRuntimeContext context, Chart chart, HashSet<string> evaluationStack)
        {
            switch (propertyName)
            {
                case "anchor":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, note.AnimatableProperties, out var anchor, out _, out _, out _, out _);
                    return new[] { anchor.X, anchor.Y };
                case "position":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, note.AnimatableProperties, out _, out var position, out _, out _, out _);
                    return new[] { position.X, position.Y };
                case "scale":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, note.AnimatableProperties, out _, out _, out var scale, out _, out _);
                    return new[] { scale.X, scale.Y };
                case "rotation":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, note.AnimatableProperties, out _, out _, out _, out var rotation, out _);
                    return rotation;
                case "opacity":
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, note.AnimatableProperties, out _, out _, out _, out _, out var opacity);
                    return opacity;
                case "kind":
                    return KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, (int)Math.Round(context.Tick, MidpointRounding.AwayFromZero), note.InitialKind).ToString();
                case "hitTime":
                    return note.HitTime;
                case "holdDuration":
                    return note.HoldDuration;
                case "customSpeed":
                    return note.CustomSpeed ?? JsValue.Null;
                case "tick":
                    return context.Tick;
                case "time":
                    return context.Time;
                case "bpm":
                    return context.Bpm;
                default:
                    return JsValue.Undefined;
            }
        }

        private static int FindLastTopLevelStatementSeparator(string expression)
        {
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;

            for (int index = expression.Length - 1; index >= 0; index--)
            {
                char current = expression[index];
                if (current == '\'' && !inDoubleQuote && !IsEscaped(expression, index))
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (current == '"' && !inSingleQuote && !IsEscaped(expression, index))
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                {
                    continue;
                }

                switch (current)
                {
                    case ')': parenDepth++; break;
                    case '(': parenDepth--; break;
                    case ']': bracketDepth++; break;
                    case '[': bracketDepth--; break;
                    case '}': braceDepth++; break;
                    case '{': braceDepth--; break;
                    case ';' when parenDepth == 0 && bracketDepth == 0 && braceDepth == 0:
                        return index;
                }
            }

            return -1;
        }

        private static string RewriteVectorSugar(string expression)
        {
            string trimmed = TrimBalancedParentheses(expression.Trim());

            int operatorIndex = FindTopLevelVectorOperator(trimmed);
            if (operatorIndex < 0)
            {
                return trimmed;
            }

            char op = trimmed[operatorIndex];
            string left = RewriteVectorSugar(trimmed[..operatorIndex]);
            string right = RewriteVectorSugar(trimmed[(operatorIndex + 1)..]);
            string helper = op == '+' ? "__ax_vecAdd" : "__ax_vecSub";
            return $"{helper}({left}, {right})";
        }

        private static int FindTopLevelVectorOperator(string expression)
        {
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;

            for (int index = expression.Length - 1; index >= 0; index--)
            {
                char current = expression[index];
                if (current == '\'' && !inDoubleQuote && !IsEscaped(expression, index))
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (current == '"' && !inSingleQuote && !IsEscaped(expression, index))
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                {
                    continue;
                }

                switch (current)
                {
                    case ')': parenDepth++; continue;
                    case '(': parenDepth--; continue;
                    case ']': bracketDepth++; continue;
                    case '[': bracketDepth--; continue;
                    case '}': braceDepth++; continue;
                    case '{': braceDepth--; continue;
                }

                if (parenDepth != 0 || bracketDepth != 0 || braceDepth != 0)
                {
                    continue;
                }

                if ((current == '+' || current == '-') && IsBinaryOperator(expression, index))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string TrimBalancedParentheses(string expression)
        {
            string current = expression;
            while (current.Length >= 2 && current[0] == '(' && current[^1] == ')' && IsFullyWrappedByParentheses(current))
            {
                current = current[1..^1].Trim();
            }

            return current;
        }

        private static bool IsFullyWrappedByParentheses(string expression)
        {
            int depth = 0;
            for (int index = 0; index < expression.Length; index++)
            {
                char current = expression[index];
                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;
                    if (depth == 0 && index < expression.Length - 1)
                    {
                        return false;
                    }
                }
            }

            return depth == 0;
        }

        private static bool IsBinaryOperator(string expression, int index)
        {
            int previousIndex = index - 1;
            while (previousIndex >= 0 && char.IsWhiteSpace(expression[previousIndex]))
            {
                previousIndex--;
            }

            if (previousIndex < 0)
            {
                return false;
            }

            char previous = expression[previousIndex];
            return previous switch
            {
                '+' or '-' or '*' or '/' or '%' or '(' or '[' or '{' or ',' or ':' or '?' or '=' or '!' or '<' or '>' or '&' or '|' => false,
                _ => true,
            };
        }

        private static bool IsEscaped(string text, int index)
        {
            int slashCount = 0;
            for (int previous = index - 1; previous >= 0 && text[previous] == '\\'; previous--)
            {
                slashCount++;
            }

            return (slashCount % 2) == 1;
        }

        private static string CreateEvaluationKey(JudgementLine line, string propertyName)
        {
            return $"{line.ID}:{propertyName}";
        }

        private sealed class ExpressionLineProxy
        {
            private readonly JudgementLine _line;
            private readonly Func<string, object> _propertyResolver;
            private readonly Func<string, object?> _noteResolver;

            public ExpressionLineProxy(JudgementLine line, Func<string, object> propertyResolver, Func<string, object?> noteResolver)
            {
                _line = line;
                _propertyResolver = propertyResolver;
                _noteResolver = noteResolver;
            }

            public string id => _line.ID;
            public string name => _line.Name;
            public string? parentId => _line.ParentLineId;
            public object anchor => _propertyResolver("anchor");
            public object position => _propertyResolver("position");
            public object scale => _propertyResolver("scale");
            public object rotation => _propertyResolver("rotation");
            public object opacity => _propertyResolver("opacity");
            public object speed => _propertyResolver("speed");

            public object? notes(string noteKey) => _noteResolver(noteKey);
        }

        private sealed class ExpressionNoteProxy
        {
            private readonly Note _note;
            private readonly Func<string, object> _propertyResolver;

            public ExpressionNoteProxy(Note note, Func<string, object> propertyResolver)
            {
                _note = note;
                _propertyResolver = propertyResolver;
            }

            public string id => _note.ID;
            public string name => _note.Name;
            public object anchor => _propertyResolver("anchor");
            public object position => _propertyResolver("position");
            public object scale => _propertyResolver("scale");
            public object rotation => _propertyResolver("rotation");
            public object opacity => _propertyResolver("opacity");
            public object kind => _propertyResolver("kind");
            public object hitTime => _propertyResolver("hitTime");
            public object holdDuration => _propertyResolver("holdDuration");
            public object customSpeed => _propertyResolver("customSpeed");
        }

        private static class ExpressionMathHelpers
        {
            public static double[] CreateVector(double x, double y) => new[] { x, y };

            public static double[] VectorAdd(object? left, object? right)
            {
                var leftVector = ConvertToVector(left);
                var rightVector = ConvertToVector(right);
                return new[] { leftVector.X + rightVector.X, leftVector.Y + rightVector.Y };
            }

            public static double[] VectorSubtract(object? left, object? right)
            {
                var leftVector = ConvertToVector(left);
                var rightVector = ConvertToVector(right);
                return new[] { leftVector.X - rightVector.X, leftVector.Y - rightVector.Y };
            }

            public static object? Multiply(object? left, object? right)
            {
                bool leftIsVector = TryConvertToVectorObject(left, out var leftVector);
                bool rightIsVector = TryConvertToVectorObject(right, out var rightVector);
                bool leftIsNumber = TryConvertToNumber(left, out var leftNumber);
                bool rightIsNumber = TryConvertToNumber(right, out var rightNumber);

                if (leftIsVector && rightIsNumber)
                {
                    return new[] { leftVector.X * rightNumber, leftVector.Y * rightNumber };
                }

                if (rightIsVector && leftIsNumber)
                {
                    return new[] { rightVector.X * leftNumber, rightVector.Y * leftNumber };
                }

                if (leftIsVector && rightIsVector)
                {
                    return new[] { leftVector.X * rightVector.X, leftVector.Y * rightVector.Y };
                }

                if (leftIsNumber && rightIsNumber)
                {
                    return leftNumber * rightNumber;
                }

                throw new InvalidOperationException("mul 只支持数字和二维向量");
            }

            private static Vector ConvertToVector(object? value)
            {
                if (TryConvertToVectorObject(value, out var vector))
                {
                    return vector;
                }

                throw new InvalidOperationException("向量运算需要 [x, y] 或 { x, y }");
            }

            private static bool TryConvertToVectorObject(object? value, out Vector vector)
            {
                vector = default;

                if (value is null)
                {
                    return false;
                }

                if (value is JsValue jsValue)
                {
                    return TryConvertToVector(jsValue, out vector);
                }

                if (value is double[] doubleArray && doubleArray.Length >= 2)
                {
                    vector = new Vector(doubleArray[0], doubleArray[1]);
                    return true;
                }

                if (value is int[] intArray && intArray.Length >= 2)
                {
                    vector = new Vector(intArray[0], intArray[1]);
                    return true;
                }

                if (value is object[] objectArray && objectArray.Length >= 2 && TryConvertToNumber(objectArray[0], out var x) && TryConvertToNumber(objectArray[1], out var y))
                {
                    vector = new Vector(x, y);
                    return true;
                }

                if (value is Vector rawVector)
                {
                    vector = rawVector;
                    return true;
                }

                return false;
            }

            private static bool TryConvertToNumber(object? value, out double number)
            {
                number = 0;

                if (value is null)
                {
                    return false;
                }

                if (value is JsValue jsValue)
                {
                    if (!jsValue.IsNumber())
                    {
                        return false;
                    }

                    number = jsValue.AsNumber();
                    return !double.IsNaN(number) && !double.IsInfinity(number);
                }

                return value switch
                {
                    double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue) => (number = doubleValue) == doubleValue,
                    float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue) => (number = floatValue) == floatValue,
                    int intValue => (number = intValue) == intValue,
                    long longValue => (number = longValue) == longValue,
                    _ => false,
                };
            }
        }
    }
}