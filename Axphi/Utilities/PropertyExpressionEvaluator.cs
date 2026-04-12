using Axphi.Data;
using Axphi.Data.KeyFrames;
using Jint;
using Jint.Native;
using Jint.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;

namespace Axphi.Utilities
{
    public readonly record struct ExpressionRuntimeContext(double Tick, double Time, double Bpm);

    public static class PropertyExpressionEvaluator
    {
        private static readonly AsyncLocal<HashSet<string>?> AmbientEvaluationStack = new();
        private static readonly AsyncLocal<BoundEvaluationState?> AmbientBoundState = new();
        private static readonly ConditionalWeakTable<Chart, ChartBindingCache> ChartBindingCaches = new();
        private static readonly object ChartBindingCacheLock = new();

        public static void InvalidateChartCache(Chart? chart)
        {
            if (chart == null)
            {
                return;
            }

            lock (ChartBindingCacheLock)
            {
                ChartBindingCaches.Remove(chart);
            }
        }

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
            if (!TryEvaluateCore(expression, baseValue, expectsVector: false, context, chart, currentLine, propertyName, evaluationStack, out var result, out error))
            {
                return false;
            }

            if (!TryConvertToDouble(result, out var convertedValue))
            {
                error = "表达式结果必须是数字";
                return false;
            }

            value = convertedValue;
            return true;
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
            if (!TryEvaluateCore(expression, new[] { baseValue.X, baseValue.Y }, expectsVector: true, context, chart, currentLine, propertyName, evaluationStack, out var result, out error))
            {
                return false;
            }

            if (!TryConvertToVector(result, out var convertedValue))
            {
                error = "二维属性表达式必须返回 [x, y]";
                return false;
            }

            value = convertedValue;
            return true;
        }

        private static bool TryEvaluateCore(
            string? expression,
            object baseValue,
            bool expectsVector,
            ExpressionRuntimeContext context,
            Chart? chart,
            JudgementLine? currentLine,
            string? propertyName,
            HashSet<string>? evaluationStack,
            out JsValue result,
            out string? error)
        {
            error = null;
            result = JsValue.Undefined;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                result = Evaluate(expression, context, baseValue, expectsVector, chart, currentLine, propertyName, evaluationStack);
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
            BoundEvaluationState? previousBoundState = AmbientBoundState.Value;
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
                    var bindingCache = GetOrCreateBindingCache(chart);
                    AmbientBoundState.Value = new BoundEvaluationState(chart, context, bindingCache);
                    BindLineReferences(engine, bindingCache, currentLine);
                }

                return engine.Evaluate($"(() => {{ {normalizedExpression} }})()");
            }
            finally
            {
                AmbientBoundState.Value = previousBoundState;
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

        private static ChartBindingCache GetOrCreateBindingCache(Chart chart)
        {
            lock (ChartBindingCacheLock)
            {
                if (ChartBindingCaches.TryGetValue(chart, out var existing)
                    && existing.LineCount == chart.JudgementLines.Count)
                {
                    return existing;
                }

                var rebuilt = BuildBindingCache(chart);
                ChartBindingCaches.Remove(chart);
                ChartBindingCaches.Add(chart, rebuilt);
                return rebuilt;
            }
        }

        private static ChartBindingCache BuildBindingCache(Chart chart)
        {
            chart.RebuildHierarchy();

            var lineLookup = new Dictionary<string, ExpressionLineProxy>(StringComparer.Ordinal);
            var lineByReference = new Dictionary<JudgementLine, ExpressionLineProxy>();

            foreach (JudgementLine line in chart.JudgementLines)
            {
                var noteLookup = BuildNoteLookup(line);
                var proxy = new ExpressionLineProxy(line, noteLookup);
                lineByReference[line] = proxy;
                RegisterLineKey(lineLookup, line.ID, proxy);
                RegisterLineKey(lineLookup, line.Name, proxy);
            }

            return new ChartBindingCache(lineLookup, lineByReference, chart.JudgementLines.Count);
        }

        private static void BindLineReferences(Engine engine, ChartBindingCache bindingCache, JudgementLine? currentLine)
        {
            var lineLookup = bindingCache.LineLookup;

            if (currentLine != null)
            {
                var selfProxy = bindingCache.LineByReference.TryGetValue(currentLine, out var cachedSelfProxy)
                    ? cachedSelfProxy
                    : new ExpressionLineProxy(currentLine, BuildNoteLookup(currentLine));
                engine.SetValue("self", selfProxy);

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

        private static Dictionary<string, Note> BuildNoteLookup(JudgementLine line)
        {
            var lookup = new Dictionary<string, Note>(StringComparer.Ordinal);
            if (line.Notes == null)
            {
                return lookup;
            }

            foreach (var note in line.Notes)
            {
                if (!string.IsNullOrWhiteSpace(note.ID) && !lookup.ContainsKey(note.ID))
                {
                    lookup[note.ID] = note;
                }

                if (!string.IsNullOrWhiteSpace(note.Name) && !lookup.ContainsKey(note.Name))
                {
                    lookup[note.Name] = note;
                }
            }

            return lookup;
        }

        private static void RegisterLineKey(Dictionary<string, ExpressionLineProxy> lookup, string? key, ExpressionLineProxy proxy)
        {
            if (string.IsNullOrWhiteSpace(key) || lookup.ContainsKey(key))
            {
                return;
            }

            lookup[key] = proxy;
        }

        private static object ResolveLineProperty(JudgementLine line, string propertyName, ExpressionRuntimeContext context, Chart chart)
        {
            var state = AmbientBoundState.Value;
            if (state == null)
            {
                return JsValue.Undefined;
            }

            context = state.Context;
            chart = state.Chart;

            if (TryResolveContextProperty(propertyName, context, out var contextValue))
            {
                return contextValue;
            }

            if (propertyName == ExpressionPropertyNames.Speed)
            {
                EasingUtils.CalculateObjectSingleTransform(context.Tick, chart.KeyFrameEasingDirection, line.InitialSpeed, line.SpeedKeyFrames, MathUtils.Lerp, line.SpeedExpressionEnabled, line.SpeedExpressionText, chart, line, out var speed);
                return speed;
            }

            return ResolveTransformProperty(
                propertyName,
                () =>
                {
                    EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, line.AnimatableProperties, chart, line, out var anchor, out var position, out var scale, out var rotation, out var opacity);
                    return (anchor, position, scale, rotation, opacity);
                });
        }

        private static object? ResolveLineNote(
            JudgementLine line,
            IReadOnlyDictionary<string, Note> noteLookup,
            string noteKey)
        {
            var state = AmbientBoundState.Value;
            if (state == null)
            {
                return null;
            }

            var context = state.Context;
            var chart = state.Chart;

            if (string.IsNullOrWhiteSpace(noteKey) || noteLookup.Count == 0)
            {
                return null;
            }

            if (!noteLookup.TryGetValue(noteKey, out var note))
            {
                return null;
            }

            state.BindingCache.LineByReference.TryGetValue(line, out var lineProxy);

            return new ExpressionNoteProxy(
                note,
                propertyName => ResolveNoteProperty(note, propertyName, context, chart),
                () => lineProxy);
        }

        private static object ResolveNoteProperty(Note note, string propertyName, ExpressionRuntimeContext context, Chart chart)
        {
            var state = AmbientBoundState.Value;
            if (state == null)
            {
                return JsValue.Undefined;
            }

            context = state.Context;
            chart = state.Chart;

            if (TryResolveContextProperty(propertyName, context, out var contextValue))
            {
                return contextValue;
            }

            return propertyName switch
            {
                ExpressionPropertyNames.Kind => KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, (int)Math.Round(context.Tick, MidpointRounding.AwayFromZero), note.InitialKind).ToString(),
                ExpressionPropertyNames.HitTime => note.HitTime,
                ExpressionPropertyNames.HoldDuration => note.HoldDuration,
                ExpressionPropertyNames.CustomSpeed => note.CustomSpeed ?? JsValue.Null,
                _ => ResolveTransformProperty(
                    propertyName,
                    () =>
                    {
                        EasingUtils.CalculateObjectTransform(context.Tick, chart.KeyFrameEasingDirection, note.AnimatableProperties, out var anchor, out var position, out var scale, out var rotation, out var opacity);
                        return (anchor, position, scale, rotation, opacity);
                    })
            };
        }

        private static bool TryResolveContextProperty(string propertyName, ExpressionRuntimeContext context, out object value)
        {
            switch (propertyName)
            {
                case ExpressionPropertyNames.Tick:
                    value = context.Tick;
                    return true;
                case ExpressionPropertyNames.Time:
                    value = context.Time;
                    return true;
                case ExpressionPropertyNames.Bpm:
                    value = context.Bpm;
                    return true;
                default:
                    value = JsValue.Undefined;
                    return false;
            }
        }

        private static object ResolveTransformProperty(
            string propertyName,
            Func<(Vector Anchor, Vector Position, Vector Scale, double Rotation, double Opacity)> resolveTransform)
        {
            if (propertyName is not (ExpressionPropertyNames.Anchor
                or ExpressionPropertyNames.Position
                or ExpressionPropertyNames.Scale
                or ExpressionPropertyNames.Rotation
                or ExpressionPropertyNames.Opacity))
            {
                return JsValue.Undefined;
            }

            var transform = resolveTransform();
            return propertyName switch
            {
                ExpressionPropertyNames.Anchor => ToVectorArray(transform.Anchor),
                ExpressionPropertyNames.Position => ToVectorArray(transform.Position),
                ExpressionPropertyNames.Scale => ToVectorArray(transform.Scale),
                ExpressionPropertyNames.Rotation => transform.Rotation,
                ExpressionPropertyNames.Opacity => transform.Opacity,
                _ => JsValue.Undefined,
            };
        }

        private static double[] ToVectorArray(Vector vector) => [vector.X, vector.Y];

        private static class ExpressionPropertyNames
        {
            public const string Anchor = "anchor";
            public const string Position = "position";
            public const string Scale = "scale";
            public const string Rotation = "rotation";
            public const string Opacity = "opacity";
            public const string Speed = "speed";
            public const string Kind = "kind";
            public const string HitTime = "hitTime";
            public const string HoldDuration = "holdDuration";
            public const string CustomSpeed = "customSpeed";
            public const string Tick = "tick";
            public const string Time = "time";
            public const string Bpm = "bpm";
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

        private sealed record BoundEvaluationState(Chart Chart, ExpressionRuntimeContext Context, ChartBindingCache BindingCache);

        private sealed record ChartBindingCache(
            Dictionary<string, ExpressionLineProxy> LineLookup,
            Dictionary<JudgementLine, ExpressionLineProxy> LineByReference,
            int LineCount);

        private sealed class ExpressionLineProxy
        {
            private readonly JudgementLine _line;
            private readonly IReadOnlyDictionary<string, Note> _noteLookup;

            public ExpressionLineProxy(JudgementLine line, IReadOnlyDictionary<string, Note> noteLookup)
            {
                _line = line;
                _noteLookup = noteLookup;
            }

            public string id => _line.ID;
            public string name => _line.Name;
            public string? parentId => _line.ParentLineId;
            public object anchor => ResolveLineProperty(_line, ExpressionPropertyNames.Anchor, default, null!);
            public object position => ResolveLineProperty(_line, ExpressionPropertyNames.Position, default, null!);
            public object scale => ResolveLineProperty(_line, ExpressionPropertyNames.Scale, default, null!);
            public object rotation => ResolveLineProperty(_line, ExpressionPropertyNames.Rotation, default, null!);
            public object opacity => ResolveLineProperty(_line, ExpressionPropertyNames.Opacity, default, null!);
            public object speed => ResolveLineProperty(_line, ExpressionPropertyNames.Speed, default, null!);

            public object? notes(string noteKey) => ResolveLineNote(_line, _noteLookup, noteKey);
        }

        private sealed class ExpressionNoteProxy
        {
            private readonly Note _note;
            private readonly Func<string, object> _propertyResolver;
            private readonly Func<object?> _lineResolver;

            public ExpressionNoteProxy(Note note, Func<string, object> propertyResolver, Func<object?> lineResolver)
            {
                _note = note;
                _propertyResolver = propertyResolver;
                _lineResolver = lineResolver;
            }

            public string id => _note.ID;
            public string name => _note.Name;
            public object? line => _lineResolver();
            public object? parent => _lineResolver();
            public object anchor => _propertyResolver(ExpressionPropertyNames.Anchor);
            public object position => _propertyResolver(ExpressionPropertyNames.Position);
            public object scale => _propertyResolver(ExpressionPropertyNames.Scale);
            public object rotation => _propertyResolver(ExpressionPropertyNames.Rotation);
            public object opacity => _propertyResolver(ExpressionPropertyNames.Opacity);
            public object kind => _propertyResolver(ExpressionPropertyNames.Kind);
            public object hitTime => _propertyResolver(ExpressionPropertyNames.HitTime);
            public object holdDuration => _propertyResolver(ExpressionPropertyNames.HoldDuration);
            public object customSpeed => _propertyResolver(ExpressionPropertyNames.CustomSpeed);
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