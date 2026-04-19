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
    private const double NoteVisibilityBufferChartUnits = 0.6;
    private const double ApproximateNoteWidthChartUnits = 1.95;
    private const double ApproximateNoteHeightChartUnits = 1.95;
    private const double MergeTolerance = 1e-6;

    private enum NoteExportStrategy
    {
        Inline,
        OffsetCarrier,
        FullCarrier,
    }

    private readonly record struct NotePlacement(OfficialNoteDto Note, bool IsAbove);
    private readonly record struct RealtimeCarrierGroup(int HitTime, double SpeedMultiplier, List<Note> Notes);
    internal readonly record struct ExportProgress(double Fraction, string Message);
    internal readonly record struct ExportOptions(bool CalculateFloorPosition);

    private sealed class ExportProgressTracker
    {
        private readonly IProgress<ExportProgress>? _progress;
        private readonly long _totalUnits;
        private readonly long _reportStride;
        private long _completedUnits;
        private long _lastReportedUnits = -1;
        private string _message = "准备导出官谱...";

        public ExportProgressTracker(IProgress<ExportProgress>? progress, long totalUnits)
        {
            _progress = progress;
            _totalUnits = Math.Max(1, totalUnits);
            _reportStride = Math.Max(1, _totalUnits / 400);
        }

        public void SetMessage(string message, bool forceReport = false)
        {
            _message = string.IsNullOrWhiteSpace(message) ? _message : message;
            if (forceReport)
            {
                Report(force: true);
            }
        }

        public void Advance(long units = 1)
        {
            _completedUnits = Math.Min(_totalUnits, _completedUnits + Math.Max(0, units));
            Report(force: false);
        }

        public void Complete(string? message = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _message = message;
            }

            _completedUnits = _totalUnits;
            Report(force: true);
        }

        private void Report(bool force)
        {
            if (_progress == null)
            {
                return;
            }

            if (!force && _completedUnits - _lastReportedUnits < _reportStride)
            {
                return;
            }

            _lastReportedUnits = _completedUnits;
            double fraction = Math.Clamp((double)_completedUnits / _totalUnits, 0.0, 1.0);
            _progress.Report(new ExportProgress(fraction, _message));
        }
    }

    public static void Export(Project project, string path)
    {
        ExportWithProgress(project, path, progress: null, options: default);
    }

    public static void ExportWithProgress(Project project, string path, IProgress<ExportProgress>? progress, ExportOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Export path cannot be empty.", nameof(path));
        }

        var tracker = new ExportProgressTracker(progress, EstimateTotalWork(project));
        tracker.SetMessage("准备导出官谱...", forceReport: true);
        var officialChart = BuildOfficialChart(project, tracker, options);
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        tracker.SetMessage("写入官谱文件...", forceReport: true);
        string json = JsonSerializer.Serialize(officialChart, jsonOptions);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        tracker.Complete("官谱导出完成");
    }

    private static OfficialChartDto BuildOfficialChart(Project project, ExportProgressTracker tracker, ExportOptions options)
    {
        var chart = project.Chart ?? new Chart();
        int endTick = GetExportEndTick(project);
        double defaultBpm = GetDefaultBpm(chart);
        var lineById = chart.JudgementLines
            .Where(line => !string.IsNullOrWhiteSpace(line.ID))
            .GroupBy(line => line.ID)
            .ToDictionary(group => group.Key, group => group.First());

        var judgeLines = new List<OfficialJudgeLineDto>();
        for (int index = 0; index < chart.JudgementLines.Count; index++)
        {
            var line = chart.JudgementLines[index];
            tracker.SetMessage($"烘焙判定线 {index + 1}/{chart.JudgementLines.Count}...", forceReport: true);
            judgeLines.AddRange(BuildExportJudgeLines(chart, line, endTick, defaultBpm, lineById, tracker));
            tracker.Advance();
        }

        if (options.CalculateFloorPosition)
        {
            tracker.SetMessage("计算 floorPosition...", forceReport: true);
            foreach (OfficialJudgeLineDto judgeLine in judgeLines)
            {
                ApplyFloorPositions(judgeLine, chart);
                tracker.Advance();
            }
        }

        double offsetSeconds = TimeTickConverter.TickToTime(
            project.Metadata.AudioOffsetTicks,
            chart.BpmKeyFrames.OrderBy(k => k.Tick).ToList(),
            defaultBpm);

        return new OfficialChartDto
        {
            FormatVersion = 3,
            Offset = Sanitize(-offsetSeconds),
            JudgeLineList = judgeLines
        };
    }

    private static long EstimateTotalWork(Project project)
    {
        Chart chart = project.Chart ?? new Chart();
        int endTick = GetExportEndTick(project);
        int maxTick = Math.Max(1, endTick);
        long total = 8;

        foreach (JudgementLine line in chart.JudgementLines)
        {
            total += 1;
            total += Math.Max(1, line.Notes.Count);
            total += 4L * maxTick;
            total += 2L * line.Notes.Count * maxTick;
        }

        return total;
    }

    private static double GetDefaultBpm(Chart chart)
    {
        if (chart.BpmKeyFrames.Count == 0)
        {
            return chart.InitialBpm;
        }

        return chart.BpmKeyFrames
            .OrderBy(k => k.Tick)
            .First()
            .Value;
    }

    private static void ApplyFloorPositions(OfficialJudgeLineDto judgeLine, Chart chart)
    {
        if (judgeLine.SpeedEvents.Count == 0)
        {
            foreach (OfficialNoteDto note in judgeLine.NotesAbove)
            {
                note.FloorPosition = 0.0;
            }

            foreach (OfficialNoteDto note in judgeLine.NotesBelow)
            {
                note.FloorPosition = 0.0;
            }

            return;
        }

        List<OfficialSpeedEventDto> orderedEvents = judgeLine.SpeedEvents.OrderBy(evt => evt.StartTime).ToList();
        double cumulativeFloorPosition = 0.0;
        foreach (OfficialSpeedEventDto speedEvent in orderedEvents)
        {
            speedEvent.FloorPosition = Sanitize(cumulativeFloorPosition);
            double segmentSeconds = TickToTimeSeconds(speedEvent.EndTime, chart) - TickToTimeSeconds(speedEvent.StartTime, chart);
            cumulativeFloorPosition += speedEvent.Value * segmentSeconds;
        }

        foreach (OfficialNoteDto note in judgeLine.NotesAbove)
        {
            note.FloorPosition = Sanitize(EvaluateFloorPositionAtTick(orderedEvents, note.Time, chart));
        }

        foreach (OfficialNoteDto note in judgeLine.NotesBelow)
        {
            note.FloorPosition = Sanitize(EvaluateFloorPositionAtTick(orderedEvents, note.Time, chart));
        }
    }

    private static double EvaluateFloorPositionAtTick(IReadOnlyList<OfficialSpeedEventDto> speedEvents, int tick, Chart chart)
    {
        OfficialSpeedEventDto speedEvent = speedEvents[^1];
        foreach (OfficialSpeedEventDto candidate in speedEvents)
        {
            if (tick < candidate.EndTime)
            {
                speedEvent = candidate;
                break;
            }
        }

        double deltaSeconds = TickToTimeSeconds(Math.Max(speedEvent.StartTime, tick), chart) - TickToTimeSeconds(speedEvent.StartTime, chart);
        return speedEvent.FloorPosition + (speedEvent.Value * Math.Max(0.0, deltaSeconds));
    }

    private static double TickToTimeSeconds(double tick, Chart chart)
    {
        return TimeTickConverter.TickToTime(tick, chart.BpmKeyFrames, chart.InitialBpm);
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
                    NoteKind kind = KeyFrameUtils.GetStepValueAtTick(note.Properties.Kind.KeyFrames, note.HitTime, note.Properties.Kind.InitialValue);
                    return note.HitTime + (kind == NoteKind.Hold ? Math.Max(0, note.HoldDuration) : 0);
                })
                .DefaultIfEmpty(0)
                .Max();
        }

        return Math.Max(1, Math.Max(metadataDuration, Math.Max(chartDuration, latestNoteTick)));
    }

    private static List<OfficialJudgeLineDto> BuildExportJudgeLines(Chart chart, JudgementLine line, int endTick, double defaultBpm, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        var notesAbove = new List<OfficialNoteDto>();
        var notesBelow = new List<OfficialNoteDto>();
        var result = new List<OfficialJudgeLineDto>();

        bool isRealtimeSpeedMode = IsRealtimeSpeedMode(line);
        var realtimeInlineGroups = new Dictionary<(int HitTime, double SpeedMultiplier), RealtimeCarrierGroup>();

        for (int noteIndex = 0; noteIndex < line.Notes.Count; noteIndex++)
        {
            var note = line.Notes[noteIndex];
            tracker.SetMessage($"分析音符 {noteIndex + 1}/{line.Notes.Count}...", forceReport: false);
            NoteExportStrategy strategy = DetermineNoteExportStrategy(note);
            if (isRealtimeSpeedMode)
            {
                bool shouldBindRealtime = ShouldBindRealtimeInlineNote(chart, line, note, lineById);
                if (strategy == NoteExportStrategy.Inline)
                {
                    if (shouldBindRealtime)
                    {
                        double speedMultiplier = Sanitize(note.Properties.Speed.InitialValue);
                        var groupKey = (note.HitTime, speedMultiplier);
                        if (!realtimeInlineGroups.TryGetValue(groupKey, out var group))
                        {
                            group = new RealtimeCarrierGroup(note.HitTime, speedMultiplier, []);
                        }

                        group.Notes.Add(note);
                        realtimeInlineGroups[groupKey] = group;
                    }
                    else
                    {
                        AddNotePlacement(BuildInlineNotePlacement(chart, line, note), notesAbove, notesBelow);
                    }

                    tracker.Advance();
                    continue;
                }

                if (shouldBindRealtime)
                {
                    result.Add(BuildCarrierJudgeLine(chart, line, note, NoteExportStrategy.FullCarrier, endTick, defaultBpm, lineById, tracker));
                }
                else
                {
                    result.Add(BuildCarrierJudgeLine(chart, line, note, strategy, endTick, defaultBpm, lineById, tracker));
                }

                tracker.Advance();
                continue;
            }

            if (strategy == NoteExportStrategy.Inline)
            {
                AddNotePlacement(BuildInlineNotePlacement(chart, line, note), notesAbove, notesBelow);
            }
            else
            {
                result.Add(BuildCarrierJudgeLine(chart, line, note, strategy, endTick, defaultBpm, lineById, tracker));
            }

            tracker.Advance();
        }

        foreach (RealtimeCarrierGroup group in realtimeInlineGroups.Values.OrderBy(group => group.HitTime).ThenBy(group => group.SpeedMultiplier))
        {
            result.Add(BuildRealtimeCarrierJudgeLine(chart, line, group, endTick, defaultBpm, lineById, tracker));
            tracker.Advance(group.Notes.Count);
        }

        notesAbove.Sort((a, b) => a.Time.CompareTo(b.Time));
        notesBelow.Sort((a, b) => a.Time.CompareTo(b.Time));

        result.Insert(0, new OfficialJudgeLineDto
        {
            Bpm = defaultBpm,
            NotesAbove = notesAbove,
            NotesBelow = notesBelow,
            SpeedEvents = BuildSpeedEvents(chart, line, endTick, tracker),
            JudgeLineMoveEvents = BuildMoveEvents(chart, line, endTick, lineById, tracker),
            JudgeLineRotateEvents = BuildRotateEvents(chart, line, endTick, lineById, tracker),
            JudgeLineDisappearEvents = BuildDisappearEvents(chart, line, endTick, tracker)
        });

        return result;
    }

    private static bool ShouldBindRealtimeInlineNote(Chart chart, JudgementLine line, Note note, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        if (!IsRealtimeSpeedMode(line))
        {
            return false;
        }

        int? visibleStartTick = FindVisibleStartTick(chart, line, note, lineById);
        if (visibleStartTick is null)
        {
            return false;
        }

        double previousSpeed = EvaluateLineSpeedAtTick(chart, line, visibleStartTick.Value);
        for (int tick = visibleStartTick.Value + 1; tick <= note.HitTime; tick++)
        {
            double speed = EvaluateLineSpeedAtTick(chart, line, tick);
            if (!AreClose(speed, previousSpeed))
            {
                return true;
            }

            previousSpeed = speed;
        }

        return false;
    }

    private static int? FindVisibleStartTick(Chart chart, JudgementLine line, Note note, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        bool seenVisible = false;
        int earliestVisibleTick = note.HitTime;

        for (int tick = note.HitTime; tick >= 0; tick--)
        {
            if (IsNoteVisibleAtTick(chart, line, note, tick, lineById))
            {
                seenVisible = true;
                earliestVisibleTick = tick;
                continue;
            }

            if (seenVisible)
            {
                break;
            }
        }

        return seenVisible ? earliestVisibleTick : null;
    }

    private static bool IsNoteVisibleAtTick(Chart chart, JudgementLine line, Note note, int tick, IReadOnlyDictionary<string, JudgementLine> lineById)
    {
        Matrix lineWorldMatrix = BuildLineWorldMatrix(line, tick, chart, lineById, new HashSet<string>(StringComparer.Ordinal));
        Matrix noteLocalMatrix = BuildNoteLocalMatrix(chart, line, note, tick, includeFall: true);
        noteLocalMatrix.Append(lineWorldMatrix);

        Rect bounds = TransformRect(noteLocalMatrix, new Rect(
            -ApproximateNoteWidthChartUnits * 0.5,
            -ApproximateNoteHeightChartUnits * 0.5,
            ApproximateNoteWidthChartUnits,
            ApproximateNoteHeightChartUnits));

        var visibleRect = new Rect(
            -8.0 - NoteVisibilityBufferChartUnits,
            -4.5 - NoteVisibilityBufferChartUnits,
            16.0 + (NoteVisibilityBufferChartUnits * 2.0),
            9.0 + (NoteVisibilityBufferChartUnits * 2.0));

        return bounds.IntersectsWith(visibleRect);
    }

    private static double EvaluateLineSpeedAtTick(Chart chart, JudgementLine line, int tick)
    {
        EasingUtils.CalculateObjectSingleTransform(
            tick,
            chart.KeyFrameEasingDirection,
            line.Properties.Speed.InitialValue,
            line.Properties.Speed.KeyFrames,
            MathUtils.Lerp,
            line.Properties.Speed.ExpressionEnabled,
            line.Properties.Speed.ExpressionText,
            chart,
            line,
            out double speed);

        return Sanitize(speed);
    }

    private static Rect TransformRect(Matrix matrix, Rect rect)
    {
        Point topLeft = matrix.Transform(rect.TopLeft);
        Point topRight = matrix.Transform(rect.TopRight);
        Point bottomLeft = matrix.Transform(rect.BottomLeft);
        Point bottomRight = matrix.Transform(rect.BottomRight);

        double minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        double maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        double minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        double maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static OfficialJudgeLineDto BuildRealtimeCarrierJudgeLine(Chart chart, JudgementLine sourceLine, RealtimeCarrierGroup group, int endTick, double defaultBpm, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        var notesAbove = new List<OfficialNoteDto>();
        var notesBelow = new List<OfficialNoteDto>();

        foreach (Note note in group.Notes)
        {
            AddNotePlacement(BuildRealtimeCarrierNotePlacement(chart, sourceLine, note), notesAbove, notesBelow);
        }

        notesAbove.Sort((a, b) => a.PositionX.CompareTo(b.PositionX));
        notesBelow.Sort((a, b) => a.PositionX.CompareTo(b.PositionX));

        return new OfficialJudgeLineDto
        {
            Bpm = defaultBpm,
            NotesAbove = notesBelow,
            NotesBelow = notesAbove,
            SpeedEvents = BuildRealtimeCarrierSpeedEvents(chart, sourceLine, group, endTick, tracker),
            JudgeLineMoveEvents = BuildRealtimeCarrierMoveEvents(chart, sourceLine, group, endTick, lineById, tracker),
            JudgeLineRotateEvents = BuildRealtimeCarrierRotateEvents(chart, sourceLine, group, endTick, lineById, tracker),
            JudgeLineDisappearEvents = BuildFlatDisappearEvents(endTick, 0.0, tracker)
        };
    }

    private static OfficialJudgeLineDto BuildCarrierJudgeLine(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, double defaultBpm, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        NotePlacement placement = BuildCarrierNotePlacement(chart, sourceLine, note, strategy);
        var notesAbove = new List<OfficialNoteDto>();
        var notesBelow = new List<OfficialNoteDto>();
        AddNotePlacement(placement, notesAbove, notesBelow);

        return new OfficialJudgeLineDto
        {
            Bpm = defaultBpm,
            NotesAbove = notesAbove,
            NotesBelow = notesBelow,
            SpeedEvents = BuildCarrierSpeedEvents(chart, sourceLine, note, strategy, endTick, tracker),
            JudgeLineMoveEvents = BuildCarrierMoveEvents(chart, sourceLine, note, strategy, endTick, lineById, tracker),
            JudgeLineRotateEvents = BuildCarrierRotateEvents(chart, sourceLine, note, strategy, endTick, lineById, tracker),
            JudgeLineDisappearEvents = BuildFlatDisappearEvents(endTick, 0.0, tracker)
        };
    }

    private static NoteExportStrategy DetermineNoteExportStrategy(Note note)
    {
        var properties = note.Properties;
        bool hasRotationChange = HasScalarPropertyChange(properties.Rotation, 0.0);
        bool hasAnchorChange = HasVectorPropertyChange(properties.Anchor, default);
        bool hasScaleChange = HasVectorPropertyChange(properties.Scale, new Vector(1, 1));
        bool hasOpacityChange = HasScalarPropertyChange(properties.Opacity, 100.0);
        bool hasOffsetExpression = properties.Position.ExpressionEnabled && !string.IsNullOrWhiteSpace(properties.Position.ExpressionText);
        bool hasOffsetMotionKeyframes = HasOffsetXMotion(note);
        bool hasOffsetYChange = !AreClose(properties.Position.InitialValue.Y, 0.0)
            || properties.Position.KeyFrames.Any(frame => !AreClose(frame.Value.Y, 0.0));

        if (hasRotationChange || hasAnchorChange || hasScaleChange || hasOpacityChange || hasOffsetExpression || hasOffsetYChange)
        {
            return NoteExportStrategy.FullCarrier;
        }

        if (hasOffsetMotionKeyframes)
        {
            return NoteExportStrategy.OffsetCarrier;
        }

        return NoteExportStrategy.Inline;
    }

    private static bool HasScalarPropertyChange(Property<double> property, double defaultValue)
    {
        return !AreClose(property.InitialValue, defaultValue)
            || (property.ExpressionEnabled && !string.IsNullOrWhiteSpace(property.ExpressionText))
            || property.KeyFrames.Any(frame => !AreClose(frame.Value, defaultValue));
    }

    private static bool HasVectorPropertyChange(Property<Vector> property, Vector defaultValue)
    {
        return !AreClose(property.InitialValue, defaultValue)
            || (property.ExpressionEnabled && !string.IsNullOrWhiteSpace(property.ExpressionText))
            || property.KeyFrames.Any(frame => !AreClose(frame.Value, defaultValue));
    }

    private static bool HasOffsetXMotion(Note note)
    {
        var offset = note.Properties.Position;
        if (offset.KeyFrames.Count <= 1)
        {
            return false;
        }

        double previousX = offset.InitialValue.X;
        foreach (KeyFrame<Vector> frame in offset.KeyFrames.OrderBy(frame => frame.Tick))
        {
            if (!AreClose(frame.Value.X, previousX))
            {
                return true;
            }

            previousX = frame.Value.X;
        }

        return false;
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

    private static NotePlacement BuildInlineNotePlacement(Chart chart, JudgementLine line, Note note)
    {
        EasingUtils.CalculateObjectTransform(
            note.HitTime,
            chart.KeyFrameEasingDirection,
            note.Properties,
            chart,
            out _,
            out Vector notePosition,
            out _,
            out _,
            out _);

        var kind = KeyFrameUtils.GetStepValueAtTick(note.Properties.Kind.KeyFrames, note.HitTime, note.Properties.Kind.InitialValue);
        return new NotePlacement(
            new OfficialNoteDto
            {
                Type = ConvertNoteKind(kind),
                Time = note.HitTime,
                HoldTime = kind == NoteKind.Hold ? Math.Max(0, note.HoldDuration) : 0,
                PositionX = ConvertProjectXToOfficialX(notePosition.X),
                Speed = EvaluateExportedNoteSpeed(chart, line, note, kind),
                FloorPosition = 0
            },
            notePosition.Y >= 0);
    }

    private static NotePlacement BuildCarrierNotePlacement(Chart chart, JudgementLine line, Note note, NoteExportStrategy strategy)
    {
        NotePlacement inlinePlacement = BuildInlineNotePlacement(chart, line, note);
        return new NotePlacement(
            new OfficialNoteDto
            {
                Type = inlinePlacement.Note.Type,
                Time = inlinePlacement.Note.Time,
                HoldTime = inlinePlacement.Note.HoldTime,
                PositionX = 0.0,
                Speed = strategy == NoteExportStrategy.FullCarrier && inlinePlacement.Note.HoldTime == 0 ? 0.0 : inlinePlacement.Note.Speed,
                FloorPosition = 0.0
            },
            strategy == NoteExportStrategy.FullCarrier ? true : inlinePlacement.IsAbove);
    }

    private static NotePlacement BuildRealtimeCarrierNotePlacement(Chart chart, JudgementLine line, Note note)
    {
        NotePlacement inlinePlacement = BuildInlineNotePlacement(chart, line, note);
        return new NotePlacement(
            new OfficialNoteDto
            {
                Type = inlinePlacement.Note.Type,
                Time = inlinePlacement.Note.Time,
                HoldTime = inlinePlacement.Note.HoldTime,
                PositionX = inlinePlacement.Note.PositionX,
                Speed = inlinePlacement.Note.HoldTime == 0 ? 0.0 : inlinePlacement.Note.Speed,
                FloorPosition = 0.0
            },
            inlinePlacement.IsAbove);
    }

    private static double EvaluateExportedNoteSpeed(Chart chart, JudgementLine line, Note note, NoteKind kind)
    {
        double speedMultiplier = Sanitize(note.Properties.Speed.InitialValue);
        if (kind != NoteKind.Hold)
        {
            return speedMultiplier;
        }

        return Sanitize(EvaluateLineSpeedAtTick(chart, line, note.HitTime) * speedMultiplier);
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

    private static List<OfficialSpeedEventDto> BuildSpeedEvents(Chart chart, JudgementLine line, int endTick, ExportProgressTracker tracker)
    {
        var speedKeyframes = line.Properties.Speed.KeyFrames.OrderBy(k => k.Tick).ToList();
        tracker.SetMessage("烘焙速度事件...", forceReport: false);
        return BakeConstantEvents(endTick, tick =>
        {
            EasingUtils.CalculateObjectSingleTransform(
                tick,
                chart.KeyFrameEasingDirection,
                line.Properties.Speed.InitialValue,
                speedKeyframes,
                MathUtils.Lerp,
                line.Properties.Speed.ExpressionEnabled,
                line.Properties.Speed.ExpressionText,
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
        }, tracker);
    }

    private static List<OfficialMoveEventDto> BuildMoveEvents(Chart chart, JudgementLine line, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙位移事件...", forceReport: false);
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
        }, tracker);
    }

    private static List<OfficialRotateEventDto> BuildRotateEvents(Chart chart, JudgementLine line, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙旋转事件...", forceReport: false);
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
        }, tracker);
    }

    private static List<OfficialDisappearEventDto> BuildDisappearEvents(Chart chart, JudgementLine line, int endTick, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙透明度事件...", forceReport: false);
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
        }, tracker);
    }

    private static List<OfficialSpeedEventDto> BuildFlatSpeedEvents(int endTick, double value, ExportProgressTracker tracker)
    {
        return BakeConstantEvents(endTick, _ => value, (startTime, endTime, speed) => new OfficialSpeedEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Value = Sanitize(speed),
            FloorPosition = 0.0
        }, tracker);
    }

    private static List<OfficialSpeedEventDto> BuildRealtimeCarrierSpeedEvents(Chart chart, JudgementLine sourceLine, RealtimeCarrierGroup group, int endTick, ExportProgressTracker tracker)
    {
        if (!GroupContainsHoldNotes(group))
        {
            return BuildFlatSpeedEvents(endTick, 0.0, tracker);
        }

        tracker.SetMessage("烘焙实时绑定线速度...", forceReport: false);
        return BakeConstantEvents(endTick, tick =>
        {
            if (tick < group.HitTime)
            {
                return 0.0;
            }

            return EvaluateLineSpeedAtTick(chart, sourceLine, tick);
        }, (startTime, endTime, speed) => new OfficialSpeedEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Value = Sanitize(speed),
            FloorPosition = 0.0
        }, tracker);
    }

    private static List<OfficialSpeedEventDto> BuildCarrierSpeedEvents(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, ExportProgressTracker tracker)
    {
        if (strategy != NoteExportStrategy.FullCarrier)
        {
            return BuildSpeedEvents(chart, sourceLine, endTick, tracker);
        }

        if (!IsHoldAtHit(note))
        {
            return BuildFlatSpeedEvents(endTick, 0.0, tracker);
        }

        tracker.SetMessage("烘焙绑定线速度...", forceReport: false);
        return BakeConstantEvents(endTick, tick =>
        {
            if (tick < note.HitTime)
            {
                return 0.0;
            }

            return EvaluateLineSpeedAtTick(chart, sourceLine, tick);
        }, (startTime, endTime, speed) => new OfficialSpeedEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Value = Sanitize(speed),
            FloorPosition = 0.0
        }, tracker);
    }

    private static List<OfficialDisappearEventDto> BuildFlatDisappearEvents(int endTick, double value, ExportProgressTracker tracker)
    {
        return BakeLinearScalarEvents(endTick, _ => value, (startTime, endTime, startValue, endValue) => new OfficialDisappearEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = Sanitize01(startValue),
            End = Sanitize01(endValue)
        }, tracker);
    }

    private static bool GroupContainsHoldNotes(RealtimeCarrierGroup group)
    {
        foreach (Note note in group.Notes)
        {
            if (IsHoldAtHit(note))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHoldAtHit(Note note)
    {
        NoteKind kind = KeyFrameUtils.GetStepValueAtTick(note.Properties.Kind.KeyFrames, note.HitTime, note.Properties.Kind.InitialValue);
        return kind == NoteKind.Hold;
    }

    private static List<OfficialMoveEventDto> BuildCarrierMoveEvents(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙音符绑定线位移...", forceReport: false);
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
        }, tracker);
    }

    private static List<OfficialRotateEventDto> BuildCarrierRotateEvents(Chart chart, JudgementLine sourceLine, Note note, NoteExportStrategy strategy, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙音符绑定线旋转...", forceReport: false);
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
        }, tracker);
    }

    private static List<OfficialMoveEventDto> BuildRealtimeCarrierMoveEvents(Chart chart, JudgementLine sourceLine, RealtimeCarrierGroup group, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙实时绑定线位移...", forceReport: false);
        return BakeLinearVectorEvents(endTick, tick =>
        {
            EvaluateRealtimeCarrierWorldTransformAtTick(chart, sourceLine, group, tick, lineById, out Vector offset, out _);
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
        }, tracker);
    }

    private static List<OfficialRotateEventDto> BuildRealtimeCarrierRotateEvents(Chart chart, JudgementLine sourceLine, RealtimeCarrierGroup group, int endTick, IReadOnlyDictionary<string, JudgementLine> lineById, ExportProgressTracker tracker)
    {
        tracker.SetMessage("烘焙实时绑定线旋转...", forceReport: false);
        return BakeLinearScalarEvents(endTick, tick =>
        {
            EvaluateRealtimeCarrierWorldTransformAtTick(chart, sourceLine, group, tick, lineById, out _, out double rotation);
            return rotation;
        }, (startTime, endTime, startValue, endValue) => new OfficialRotateEventDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Start = Sanitize(startValue),
            End = Sanitize(endValue)
        }, tracker);
    }

    private static List<TEvent> BakeConstantEvents<TEvent>(int endTick, Func<int, double> sampleAtTick, Func<int, int, double, TEvent> createEvent, ExportProgressTracker tracker)
    {
        int maxTick = Math.Max(1, endTick);
        var result = new List<TEvent>();

        int segmentStart = 0;
        double segmentValue = Sanitize(sampleAtTick(0));

        for (int tick = 1; tick < maxTick; tick++)
        {
            double value = Sanitize(sampleAtTick(tick));
            tracker.Advance();
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

    private static List<TEvent> BakeLinearScalarEvents<TEvent>(int endTick, Func<int, double> sampleAtTick, Func<int, int, double, double, TEvent> createEvent, ExportProgressTracker tracker)
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
            tracker.Advance();
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

    private static List<TEvent> BakeLinearVectorEvents<TEvent>(int endTick, Func<int, Vector> sampleAtTick, Func<int, int, Vector, Vector, TEvent> createEvent, ExportProgressTracker tracker)
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
            tracker.Advance();
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

    private static bool IsRealtimeSpeedMode(JudgementLine line)
    {
        return string.Equals(line.SpeedMode, "Realtime", StringComparison.OrdinalIgnoreCase);
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

    private static void EvaluateRealtimeCarrierWorldTransformAtTick(Chart chart, JudgementLine sourceLine, RealtimeCarrierGroup group, int tick, IReadOnlyDictionary<string, JudgementLine> lineById, out Vector offset, out double rotation)
    {
        Matrix lineWorldMatrix = BuildLineWorldMatrix(sourceLine, tick, chart, lineById, new HashSet<string>(StringComparer.Ordinal));
        Matrix carrierLocalMatrix = BuildRealtimeCarrierLocalMatrix(chart, sourceLine, group, tick);
        carrierLocalMatrix.Append(lineWorldMatrix);

        Point worldOrigin = carrierLocalMatrix.Transform(new Point(0, 0));
        Vector worldAxis = carrierLocalMatrix.Transform(new Vector(1, 0));

        offset = new Vector(worldOrigin.X, -worldOrigin.Y);
        rotation = worldAxis.LengthSquared <= MergeTolerance
            ? 0.0
            : Sanitize(-Math.Atan2(worldAxis.Y, worldAxis.X) * 180.0 / Math.PI);
    }

    private static Matrix BuildRealtimeCarrierLocalMatrix(Chart chart, JudgementLine sourceLine, RealtimeCarrierGroup group, double currentTick)
    {
        double fallDistance = CalculateTravelDistanceChartUnits(chart, sourceLine, currentTick, group.HitTime, group.SpeedMultiplier);
        var localTransform = new TransformGroup
        {
            Children =
            {
                new TranslateTransform(0.0, fallDistance)
            }
        };

        return localTransform.Value;
    }

    private static Matrix BuildNoteLocalMatrix(Chart chart, JudgementLine sourceLine, Note note, double currentTick, bool includeFall)
    {
        EasingUtils.CalculateObjectTransform(
            currentTick,
            chart.KeyFrameEasingDirection,
            note.Properties,
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
        return CalculateTravelDistanceChartUnits(chart, line, currentTick, note.HitTime, note.Properties.Speed.InitialValue);
    }

    private static double CalculateTravelDistanceChartUnits(Chart chart, JudgementLine line, double currentTick, int hitTime, double noteSpeedMultiplier)
    {
        if (currentTick >= hitTime)
        {
            return 0.0;
        }

        if (IsRealtimeSpeedMode(line))
        {
            double currentSeconds = TimeTickConverter.TickToTime(currentTick, chart.BpmKeyFrames, chart.InitialBpm);
            double hitTimeSeconds = TimeTickConverter.TickToTime(hitTime, chart.BpmKeyFrames, chart.InitialBpm);
            EasingUtils.CalculateObjectSingleTransform(
                currentTick,
                chart.KeyFrameEasingDirection,
                line.Properties.Speed.InitialValue,
                line.Properties.Speed.KeyFrames,
                MathUtils.Lerp,
                line.Properties.Speed.ExpressionEnabled,
                line.Properties.Speed.ExpressionText,
                chart,
                line,
                out double currentRealtimeSpeed);

            return -BaseVerticalFlowChartUnitsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier * (hitTimeSeconds - currentSeconds);
        }

        return -CalculateIntegralDistanceChartUnits(currentTick, hitTime, line, chart, noteSpeedMultiplier);
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
                line.Properties.Speed.InitialValue,
                line.Properties.Speed.KeyFrames,
                MathUtils.Lerp,
                line.Properties.Speed.ExpressionEnabled,
                line.Properties.Speed.ExpressionText,
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
            line.Properties,
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
            line.Properties,
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