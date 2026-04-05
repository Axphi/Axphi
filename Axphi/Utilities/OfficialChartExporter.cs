using Axphi.Data;
using Axphi.Data.AnimatableProperties;
using Axphi.Data.KeyFrames;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace Axphi.Utilities;

internal static class OfficialChartExporter
{
    private const double ProjectViewportWidthUnits = 16.0;
    private const double ProjectViewportHeightUnits = 9.0;
    private const double OfficialNoteXUnitSpan = 18.0;
    private const double BaseVerticalFlowChartUnitsPerSecond = 5.4;
    private const double MergeTolerance = 1e-6;

    private enum NoteExportStrategy
    {
        Inline,
        OffsetCarrier,
        FullCarrier,
    }

    private readonly record struct NotePlacement(OfficialNoteDto Note, bool IsAbove);

    public static void Export(Project project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Export path cannot be empty.", nameof(path));
        }

        var officialChart = BuildOfficialChart(project);
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(officialChart, jsonOptions);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static OfficialChartDto BuildOfficialChart(Project project)
    {
        var chart = project.Chart ?? new Chart();
        int endTick = GetExportEndTick(project);
        double defaultBpm = GetDefaultBpm(chart);
        var lineById = chart.JudgementLines
            .Where(line => !string.IsNullOrWhiteSpace(line.ID))
            .GroupBy(line => line.ID)
            .ToDictionary(group => group.Key, group => group.First());

        var judgeLines = new List<OfficialJudgeLineDto>();
        foreach (var line in chart.JudgementLines)
        {
            judgeLines.AddRange(BuildExportJudgeLines(chart, line, endTick, defaultBpm, lineById));
        }

        double offsetSeconds = TimeTickConverter.TickToTime(
            project.Metadata.AudioOffsetTicks,
            chart.BpmKeyFrames.OrderBy(k => k.Time).ToList(),
            defaultBpm);

        return new OfficialChartDto
        {
            FormatVersion = 3,
            Offset = Sanitize(offsetSeconds),
            JudgeLineList = judgeLines
        };
    }

    private static double GetDefaultBpm(Chart chart)
    {
        if (chart.BpmKeyFrames.Count == 0)
        {
            return chart.InitialBpm;
        }

        return chart.BpmKeyFrames
            .OrderBy(k => k.Time)
            .First()
            .Value;
    }

    private static int GetExportEndTick(Project project)
    {
        int metadataDuration = project.Metadata?.TotalDurationTicks ?? 0;
        int chartDuration = project.Chart?.Duration ?? 0;
        int latestNoteTick = 0;

        if (project.Chart is not null)
        {
            latestNoteTick = project.Chart.JudgementLines
                .SelectMany(line => line.Notes)
                .Select(note =>
                {
                    NoteKind kind = KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, note.HitTime, note.InitialKind);
                    return note.HitTime + (kind == NoteKind.Hold ? Math.Max(0, note.HoldDuration) : 0);
                })
                .DefaultIfEmpty(0)
                .Max();
        }

        return Math.Max(1, Math.Max(metadataDuration, Math.Max(chartDuration, latestNoteTick)));
    }

    private static List<OfficialJudgeLineDto> BuildExportJudgeLines(Chart chart, JudgementLine line, int endTick, double defaultBpm, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        var notesAbove = new List<OfficialNoteDto>();
        var notesBelow = new List<OfficialNoteDto>();
        var result = new List<OfficialJudgeLineDto>();

        foreach (var note in line.Notes)
        {
            NoteExportStrategy strategy = DetermineNoteExportStrategy(note);
            if (strategy == NoteExportStrategy.Inline)
            {
                AddNotePlacement(BuildInlineNotePlacement(chart, note), notesAbove, notesBelow);
            }
            else
            {
                result.Add(BuildCarrierJudgeLine(chart, line, note, strategy, endTick, defaultBpm, lineById));
            }
        }

        notesAbove.Sort((a, b) => a.Time.CompareTo(b.Time));
        notesBelow.Sort((a, b) => a.Time.CompareTo(b.Time));

        result.Insert(0, new OfficialJudgeLineDto
        {
            Bpm = defaultBpm,
            NotesAbove = notesAbove,
            NotesBelow = notesBelow,
            SpeedEvents = BuildSpeedEvents(chart, line, endTick),
            JudgeLineMoveEvents = BuildMoveEvents(chart, line, endTick, lineById),
            JudgeLineRotateEvents = BuildRotateEvents(chart, line, endTick, lineById),
            JudgeLineDisappearEvents = BuildDisappearEvents(chart, line, endTick)
        });

        return result;
    }

    private static OfficialJudgeLineDto BuildCarrierJudgeLine(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, double defaultBpm, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        NotePlacement placement = BuildCarrierNotePlacement(chart, note, strategy);
        var notesAbove = new List<OfficialNoteDto>();
        var notesBelow = new List<OfficialNoteDto>();
        AddNotePlacement(placement, notesAbove, notesBelow);

        return new OfficialJudgeLineDto
        {
            Bpm = defaultBpm,
            NotesAbove = notesAbove,
            NotesBelow = notesBelow,
            SpeedEvents = strategy == NoteExportStrategy.FullCarrier
                ? BuildFlatSpeedEvents(endTick, 0.0)
                : BuildSpeedEvents(chart, sourceLine, endTick),
            JudgeLineMoveEvents = BuildCarrierMoveEvents(chart, sourceLine, note, strategy, endTick, lineById),
            JudgeLineRotateEvents = BuildCarrierRotateEvents(chart, sourceLine, note, strategy, endTick, lineById),
            JudgeLineDisappearEvents = BuildFlatDisappearEvents(endTick, 0.0)
        };
    }

    private static NoteExportStrategy DetermineNoteExportStrategy(Note note)
    {
        var properties = note.AnimatableProperties;
        bool hasRotationChange = HasScalarPropertyChange(properties.Rotation, 0.0);
        bool hasAnchorChange = HasVectorPropertyChange(properties.Anchor, default);
        bool hasScaleChange = HasVectorPropertyChange(properties.Scale, new Vector(1, 1));
        bool hasOpacityChange = HasScalarPropertyChange(properties.Opacity, 100.0);
        bool hasOffsetExpression = properties.Offset.ExpressionEnabled && !string.IsNullOrWhiteSpace(properties.Offset.ExpressionText);
        bool hasOffsetKeyframes = properties.Offset.KeyFrames.Count > 0;
        bool hasOffsetYChange = !AreClose(properties.Offset.InitialValue.Y, 0.0)
            || properties.Offset.KeyFrames.Any(frame => !AreClose(frame.Value.Y, 0.0));

        if (hasRotationChange || hasAnchorChange || hasScaleChange || hasOpacityChange || hasOffsetExpression || hasOffsetYChange)
        {
            return NoteExportStrategy.FullCarrier;
        }

        if (hasOffsetKeyframes)
        {
            return NoteExportStrategy.OffsetCarrier;
        }

        return NoteExportStrategy.Inline;
    }

    private static bool HasScalarPropertyChange<TKeyFrame>(AnimatableProperty<double, TKeyFrame> property, double defaultValue)
        where TKeyFrame : KeyFrame<double>
    {
        return !AreClose(property.InitialValue, defaultValue)
            || (property.ExpressionEnabled && !string.IsNullOrWhiteSpace(property.ExpressionText))
            || property.KeyFrames.Any(frame => !AreClose(frame.Value, defaultValue));
    }

    private static bool HasVectorPropertyChange<TKeyFrame>(AnimatableProperty<Vector, TKeyFrame> property, Vector defaultValue)
        where TKeyFrame : KeyFrame<Vector>
    {
        return !AreClose(property.InitialValue, defaultValue)
            || (property.ExpressionEnabled && !string.IsNullOrWhiteSpace(property.ExpressionText))
            || property.KeyFrames.Any(frame => !AreClose(frame.Value, defaultValue));
    }

    private static void AddNotePlacement(NotePlacement placement, List<OfficialNoteDto> notesAbove, List<OfficialNoteDto> notesBelow)
    {
        if (placement.IsAbove)
        {
            notesAbove.Add(placement.Note);
        }
        else
        {
            notesBelow.Add(placement.Note);
        }
    }

    private static NotePlacement BuildInlineNotePlacement(Chart chart, Note note)
    {
        EasingUtils.CalculateObjectTransform(
            note.HitTime,
            chart.KeyFrameEasingDirection,
            note.AnimatableProperties,
            chart,
            out _,
            out Vector notePosition,
            out _,
            out _,
            out _);

        var kind = KeyFrameUtils.GetStepValueAtTick(note.KindKeyFrames, note.HitTime, note.InitialKind);
        return new NotePlacement(
            new OfficialNoteDto
            {
                Type = ConvertNoteKind(kind),
                Time = note.HitTime,
                HoldTime = kind == NoteKind.Hold ? Math.Max(0, note.HoldDuration) : 0,
                PositionX = ConvertProjectXToOfficialX(notePosition.X),
                Speed = Sanitize(note.CustomSpeed ?? 1.0),
                FloorPosition = 0
            },
            notePosition.Y >= 0);
    }

    private static NotePlacement BuildCarrierNotePlacement(Chart chart, Note note, NoteExportStrategy strategy)
    {
        NotePlacement inlinePlacement = BuildInlineNotePlacement(chart, note);
        return new NotePlacement(
            new OfficialNoteDto
            {
                Type = inlinePlacement.Note.Type,
                Time = inlinePlacement.Note.Time,
                HoldTime = inlinePlacement.Note.HoldTime,
                PositionX = 0.0,
                Speed = strategy == NoteExportStrategy.FullCarrier ? 0.0 : inlinePlacement.Note.Speed,
                FloorPosition = 0.0
            },
            strategy == NoteExportStrategy.FullCarrier ? true : inlinePlacement.IsAbove);
    }

    private static int ConvertNoteKind(NoteKind kind)
    {
        return kind switch
        {
            NoteKind.Tap => 1,
            NoteKind.Drag => 2,
            NoteKind.Hold => 3,
            NoteKind.Flick => 4,
            _ => 1
        };
    }

    private static List<OfficialSpeedEventDto> BuildSpeedEvents(Chart chart, JudgementLine line, int endTick)
    {
        var speedKeyframes = line.SpeedKeyFrames.OrderBy(k => k.Time).ToList();
        return BakeConstantEvents(endTick, tick =>
        {
            EasingUtils.CalculateObjectSingleTransform(
                tick,
                chart.KeyFrameEasingDirection,
                line.InitialSpeed,
                speedKeyframes,
                MathUtils.Lerp,
                line.SpeedExpressionEnabled,
                line.SpeedExpressionText,
                chart,
                line,
                out double speed);

            return speed;
        }, (startTime, endTime, speed) => new OfficialSpeedEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Value = Sanitize(speed),
            FloorPosition = 0
        });
    }

    private static List<OfficialMoveEventDto> BuildMoveEvents(Chart chart, JudgementLine line, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        return BakeLinearVectorEvents(endTick, tick =>
        {
            EvaluateLineWorldTransformAtTick(chart, line, tick, lineById, out Vector offset, out _);
            return new Vector(
                NormalizeProjectXToViewport(offset.X),
                NormalizeProjectYToViewport(offset.Y));
        }, (startTime, endTime, startValue, endValue) => new OfficialMoveEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = startValue.X,
            End = endValue.X,
            Start2 = startValue.Y,
            End2 = endValue.Y
        });
    }

    private static List<OfficialRotateEventDto> BuildRotateEvents(Chart chart, JudgementLine line, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        return BakeLinearScalarEvents(endTick, tick =>
        {
            EvaluateLineWorldTransformAtTick(chart, line, tick, lineById, out _, out double rotation);
            return rotation;
        }, (startTime, endTime, startValue, endValue) => new OfficialRotateEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = Sanitize(startValue),
            End = Sanitize(endValue)
        });
    }

    private static List<OfficialDisappearEventDto> BuildDisappearEvents(Chart chart, JudgementLine line, int endTick)
    {
        return BakeLinearScalarEvents(endTick, tick =>
        {
            EvaluateLineTransformAtTick(chart, line, tick, out _, out _, out double opacity);
            return Sanitize01(opacity / 100.0);
        }, (startTime, endTime, startValue, endValue) => new OfficialDisappearEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = startValue,
            End = endValue
        });
    }

    private static List<OfficialSpeedEventDto> BuildFlatSpeedEvents(int endTick, double value)
    {
        return BakeConstantEvents(endTick, _ => value, (startTime, endTime, speed) => new OfficialSpeedEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Value = Sanitize(speed),
            FloorPosition = 0.0
        });
    }

    private static List<OfficialDisappearEventDto> BuildFlatDisappearEvents(int endTick, double value)
    {
        return BakeLinearScalarEvents(endTick, _ => value, (startTime, endTime, startValue, endValue) => new OfficialDisappearEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = Sanitize01(startValue),
            End = Sanitize01(endValue)
        });
    }

    private static List<OfficialMoveEventDto> BuildCarrierMoveEvents(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        return BakeLinearVectorEvents(endTick, tick =>
        {
            EvaluateCarrierWorldTransformAtTick(chart, sourceLine, note, strategy, tick, lineById, out Vector offset, out _);
            return new Vector(
                NormalizeProjectXToViewport(offset.X),
                NormalizeProjectYToViewport(offset.Y));
        }, (startTime, endTime, startValue, endValue) => new OfficialMoveEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = startValue.X,
            End = endValue.X,
            Start2 = startValue.Y,
            End2 = endValue.Y
        });
    }

    private static List<OfficialRotateEventDto> BuildCarrierRotateEvents(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        return BakeLinearScalarEvents(endTick, tick =>
        {
            EvaluateCarrierWorldTransformAtTick(chart, sourceLine, note, strategy, tick, lineById, out _, out double rotation);
            return rotation;
        }, (startTime, endTime, startValue, endValue) => new OfficialRotateEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = Sanitize(startValue),
            End = Sanitize(endValue)
        });
    }

    private static List<TEvent> BakeConstantEvents<TEvent>(int endTick, Func<int, double> sampleAtTick, Func<int, int, double, TEvent> createEvent)
    {
        int maxTick = Math.Max(1, endTick);
        var result = new List<TEvent>();

        int segmentStart = 0;
        double segmentValue = Sanitize(sampleAtTick(0));

        for (int tick = 1; tick < maxTick; tick++)
        {
            double value = Sanitize(sampleAtTick(tick));
            if (AreClose(value, segmentValue))
            {
                continue;
            }

            result.Add(createEvent(segmentStart, tick, segmentValue));
            segmentStart = tick;
            segmentValue = value;
        }

        result.Add(createEvent(segmentStart, maxTick, segmentValue));
        return result;
    }

    private static List<TEvent> BakeLinearScalarEvents<TEvent>(int endTick, Func<int, double> sampleAtTick, Func<int, int, double, double, TEvent> createEvent)
    {
        int maxTick = Math.Max(1, endTick);
        var result = new List<TEvent>();

        double segmentStartValue = Sanitize(sampleAtTick(0));
        if (maxTick == 1)
        {
            result.Add(createEvent(0, 1, segmentStartValue, Sanitize(sampleAtTick(1))));
            return result;
        }

        int segmentStart = 0;
        double previousValue = Sanitize(sampleAtTick(1));
        double slope = previousValue - segmentStartValue;

        for (int tick = 2; tick <= maxTick; tick++)
        {
            double value = Sanitize(sampleAtTick(tick));
            double expectedValue = segmentStartValue + (slope * (tick - segmentStart));

            if (!AreClose(value, expectedValue))
            {
                int segmentEnd = tick - 1;
                result.Add(createEvent(segmentStart, segmentEnd, segmentStartValue, previousValue));

                segmentStart = segmentEnd;
                segmentStartValue = previousValue;
                slope = value - segmentStartValue;
            }

            previousValue = value;
        }

        result.Add(createEvent(segmentStart, maxTick, segmentStartValue, previousValue));
        return result;
    }

    private static List<TEvent> BakeLinearVectorEvents<TEvent>(int endTick, Func<int, Vector> sampleAtTick, Func<int, int, Vector, Vector, TEvent> createEvent)
    {
        int maxTick = Math.Max(1, endTick);
        var result = new List<TEvent>();

        Vector segmentStartValue = SanitizeVector(sampleAtTick(0));
        if (maxTick == 1)
        {
            result.Add(createEvent(0, 1, segmentStartValue, SanitizeVector(sampleAtTick(1))));
            return result;
        }

        int segmentStart = 0;
        Vector previousValue = SanitizeVector(sampleAtTick(1));
        Vector slope = Subtract(previousValue, segmentStartValue);

        for (int tick = 2; tick <= maxTick; tick++)
        {
            Vector value = SanitizeVector(sampleAtTick(tick));
            Vector expectedValue = Add(segmentStartValue, Multiply(slope, tick - segmentStart));

            if (!AreClose(value, expectedValue))
            {
                int segmentEnd = tick - 1;
                result.Add(createEvent(segmentStart, segmentEnd, segmentStartValue, previousValue));

                segmentStart = segmentEnd;
                segmentStartValue = previousValue;
                slope = Subtract(value, segmentStartValue);
            }

            previousValue = value;
        }

        result.Add(createEvent(segmentStart, maxTick, segmentStartValue, previousValue));
        return result;
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) <= MergeTolerance;
    }

    private static bool AreClose(Vector left, Vector right)
    {
        return AreClose(left.X, right.X) && AreClose(left.Y, right.Y);
    }

    private static void EvaluateCarrierWorldTransformAtTick(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int tick, IReadOnlyDictionary<string, JudgementLine> lineById, out Vector offset, out double rotation)
    {
        Matrix lineWorldMatrix = BuildLineWorldMatrix(sourceLine, tick, chart, lineById, new HashSet<string>(StringComparer.Ordinal));
        Matrix noteLocalMatrix = BuildNoteLocalMatrix(chart, sourceLine, note, tick, includeFall: strategy == NoteExportStrategy.FullCarrier);
        noteLocalMatrix.Append(lineWorldMatrix);

        Point worldOrigin = noteLocalMatrix.Transform(new Point(0, 0));
        Vector worldAxis = noteLocalMatrix.Transform(new Vector(1, 0));

        offset = new Vector(worldOrigin.X, -worldOrigin.Y);
        rotation = worldAxis.LengthSquared <= MergeTolerance
            ? 0.0
            : Sanitize(-Math.Atan2(worldAxis.Y, worldAxis.X) * 180.0 / Math.PI);
    }

    private static Matrix BuildNoteLocalMatrix(Chart chart, JudgementLine sourceLine, Note note, double currentTick, bool includeFall)
    {
        EasingUtils.CalculateObjectTransform(
            currentTick,
            chart.KeyFrameEasingDirection,
            note.AnimatableProperties,
            out var anchor,
            out var offset,
            out var scale,
            out var rotationAngle,
            out _);

        double fallDistance = includeFall
            ? CalculateNoteTravelDistanceChartUnits(chart, sourceLine, currentTick, note)
            : 0.0;

        var localTransform = new TransformGroup
        {
            Children =
            {
                new TranslateTransform(-anchor.X, -anchor.Y),
                new ScaleTransform(scale.X, scale.Y),
                new RotateTransform(rotationAngle),
                new TranslateTransform(anchor.X, anchor.Y),
                new TranslateTransform(offset.X, offset.Y + fallDistance),
            }
        };

        return localTransform.Value;
    }

    private static double CalculateNoteTravelDistanceChartUnits(Chart chart, JudgementLine line, double currentTick, Note note)
    {
        double noteSpeedMultiplier = note.CustomSpeed ?? 1.0;

        if (line.SpeedMode == "Realtime")
        {
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            double hitTimeSeconds = TimeTickConverter.TickToTime(note.HitTime, chart.BpmKeyFrames, chart.InitialBpm);
            EasingUtils.CalculateObjectSingleTransform(
                currentTick,
                chart.KeyFrameEasingDirection,
                line.InitialSpeed,
                line.SpeedKeyFrames,
                MathUtils.Lerp,
                line.SpeedExpressionEnabled,
                line.SpeedExpressionText,
                chart,
                line,
                out double currentRealtimeSpeed);

            return -BaseVerticalFlowChartUnitsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier * (hitTimeSeconds - currentSeconds);
        }

        return -CalculateIntegralDistanceChartUnits(currentTick, note.HitTime, line, chart, noteSpeedMultiplier);
    }

    private static double CalculateIntegralDistanceChartUnits(double startTick, double endTick, JudgementLine line, Chart chart, double noteSpeedMultiplier)
    {
        if (Math.Abs(startTick - endTick) < double.Epsilon)
        {
            return 0.0;
        }

        int steps = 150;
        double totalDistance = 0.0;
        double tMin = Math.Min(startTick, endTick);
        double tMax = Math.Max(startTick, endTick);
        double stepTick = (tMax - tMin) / steps;

        for (int index = 0; index < steps; index++)
        {
            double t1 = tMin + index * stepTick;
            double t2 = tMin + (index + 1) * stepTick;
            double sec1 = TimeTickConverter.TickToTime(t1, chart.BpmKeyFrames, chart.InitialBpm);
            double sec2 = TimeTickConverter.TickToTime(t2, chart.BpmKeyFrames, chart.InitialBpm);
            double midTick = (t1 + t2) * 0.5;

            EasingUtils.CalculateObjectSingleTransform(
                midTick,
                chart.KeyFrameEasingDirection,
                line.InitialSpeed,
                line.SpeedKeyFrames,
                MathUtils.Lerp,
                line.SpeedExpressionEnabled,
                line.SpeedExpressionText,
                chart,
                line,
                out double midSpeed);

            totalDistance += midSpeed * (sec2 - sec1);
        }

        double chartUnitDistance = totalDistance * BaseVerticalFlowChartUnitsPerSecond * noteSpeedMultiplier;
        return startTick <= endTick ? chartUnitDistance : -chartUnitDistance;
    }

    private static Vector SanitizeVector(Vector value)
    {
        return new Vector(Sanitize(value.X), Sanitize(value.Y));
    }

    private static Vector Add(Vector left, Vector right)
    {
        return new Vector(left.X + right.X, left.Y + right.Y);
    }

    private static Vector Subtract(Vector left, Vector right)
    {
        return new Vector(left.X - right.X, left.Y - right.Y);
    }

    private static Vector Multiply(Vector value, double scalar)
    {
        return new Vector(value.X * scalar, value.Y * scalar);
    }

    private static void EvaluateLineTransformAtTick(Chart chart, JudgementLine line, int tick, out Vector offset, out double rotation, out double opacity)
    {
        EasingUtils.CalculateObjectTransform(
            tick,
            chart.KeyFrameEasingDirection,
            line.AnimatableProperties,
            chart,
            line,
            out _,
            out offset,
            out _,
            out rotation,
            out opacity);
    }

    private static void EvaluateLineWorldTransformAtTick(Chart chart, JudgementLine line, int tick, IReadOnlyDictionary<string, JudgementLine> lineById, out Vector offset, out double rotation)
    {
        Matrix worldMatrix = BuildLineWorldMatrix(line, tick, chart, lineById, new HashSet<string>(StringComparer.Ordinal));
        Point worldOrigin = worldMatrix.Transform(new Point(0, 0));
        Vector worldAxis = worldMatrix.Transform(new Vector(1, 0));

        offset = new Vector(worldOrigin.X, -worldOrigin.Y);
        rotation = worldAxis.LengthSquared <= MergeTolerance
            ? 0.0
            : Sanitize(-Math.Atan2(worldAxis.Y, worldAxis.X) * 180.0 / Math.PI);
    }

    private static Matrix BuildLineWorldMatrix(JudgementLine line, double currentTick, Chart chart, IReadOnlyDictionary<string, JudgementLine> lineById, HashSet<string> visiting)
    {
        Matrix localMatrix = BuildLineLocalMatrix(line, currentTick, chart);

        if (string.IsNullOrWhiteSpace(line.ParentLineId)
            || line.ParentLineId == line.ID
            || !visiting.Add(line.ID))
        {
            return localMatrix;
        }

        if (!lineById.TryGetValue(line.ParentLineId, out var parentLine))
        {
            visiting.Remove(line.ID);
            return localMatrix;
        }

        Matrix parentMatrix = BuildLineWorldMatrix(parentLine, currentTick, chart, lineById, visiting);
        localMatrix.Append(parentMatrix);

        visiting.Remove(line.ID);
        return localMatrix;
    }

    private static Matrix BuildLineLocalMatrix(JudgementLine line, double currentTick, Chart chart)
    {
        EasingUtils.CalculateObjectTransform(
            currentTick,
            chart.KeyFrameEasingDirection,
            line.AnimatableProperties,
            chart,
            line,
            out var anchor,
            out var offset,
            out var scale,
            out var rotationAngle,
            out _);

        var screenAnchor = new Vector(anchor.X, -anchor.Y);
        var screenOffset = new Vector(offset.X, -offset.Y);

        var localTransform = new TransformGroup
        {
            Children =
            {
                new TranslateTransform(-screenAnchor.X, -screenAnchor.Y),
                new ScaleTransform(scale.X, scale.Y),
                new RotateTransform(-rotationAngle),
                new TranslateTransform(screenAnchor.X, screenAnchor.Y),
                new TranslateTransform(screenOffset.X, screenOffset.Y),
            }
        };

        return localTransform.Value;
    }

    private static double Sanitize(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }

    private static double ConvertProjectXToOfficialX(double projectX)
    {
        return Sanitize(projectX * (OfficialNoteXUnitSpan / ProjectViewportWidthUnits));
    }

    private static double NormalizeProjectXToViewport(double projectX)
    {
        return Sanitize((projectX / ProjectViewportWidthUnits) + 0.5);
    }

    private static double NormalizeProjectYToViewport(double projectY)
    {
        return Sanitize((projectY / ProjectViewportHeightUnits) + 0.5);
    }

    private static double Sanitize01(double value)
    {
        return Math.Clamp(Sanitize(value), 0.0, 1.0);
    }

    private sealed class OfficialChartDto
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("judgeLineList")]
        public List<OfficialJudgeLineDto> JudgeLineList { get; set; } = new();
    }

    private sealed class OfficialJudgeLineDto
    {
        [JsonPropertyName("bpm")]
        public double Bpm { get; set; }

        [JsonPropertyName("notesAbove")]
        public List<OfficialNoteDto> NotesAbove { get; set; } = new();

        [JsonPropertyName("notesBelow")]
        public List<OfficialNoteDto> NotesBelow { get; set; } = new();

        [JsonPropertyName("speedEvents")]
        public List<OfficialSpeedEventDto> SpeedEvents { get; set; } = new();

        [JsonPropertyName("judgeLineMoveEvents")]
        public List<OfficialMoveEventDto> JudgeLineMoveEvents { get; set; } = new();

        [JsonPropertyName("judgeLineRotateEvents")]
        public List<OfficialRotateEventDto> JudgeLineRotateEvents { get; set; } = new();

        [JsonPropertyName("judgeLineDisappearEvents")]
        public List<OfficialDisappearEventDto> JudgeLineDisappearEvents { get; set; } = new();
    }

    private sealed class OfficialNoteDto
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("time")]
        public int Time { get; set; }

        [JsonPropertyName("holdTime")]
        public int HoldTime { get; set; }

        [JsonPropertyName("positionX")]
        public double PositionX { get; set; }

        [JsonPropertyName("speed")]
        public double Speed { get; set; }

        [JsonPropertyName("floorPosition")]
        public double FloorPosition { get; set; }
    }

    private sealed class OfficialSpeedEventDto
    {
        [JsonPropertyName("startTime")]
        public int StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public int EndTime { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("floorPosition")]
        public double FloorPosition { get; set; }
    }

    private sealed class OfficialMoveEventDto
    {
        [JsonPropertyName("startTime")]
        public int StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public int EndTime { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("start2")]
        public double Start2 { get; set; }

        [JsonPropertyName("end2")]
        public double End2 { get; set; }
    }

    private sealed class OfficialRotateEventDto
    {
        [JsonPropertyName("startTime")]
        public int StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public int EndTime { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }
    }

    private sealed class OfficialDisappearEventDto
    {
        [JsonPropertyName("startTime")]
        public int StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public int EndTime { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }
    }
}
