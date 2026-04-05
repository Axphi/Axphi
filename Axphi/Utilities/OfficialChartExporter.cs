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

namespace Axphi.Utilities;

internal static class OfficialChartExporter
{
    private const double ProjectViewportWidthUnits = 16.0;
    private const double ProjectViewportHeightUnits = 9.0;
    private const double OfficialNoteXUnitSpan = 18.0;

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

        var judgeLines = new List<OfficialJudgeLineDto>();
        foreach (var line in chart.JudgementLines)
        {
            BuildLineNotes(chart, line, out var notesAbove, out var notesBelow);
            var lineProperties = line.AnimatableProperties;

            judgeLines.Add(new OfficialJudgeLineDto
            {
                Bpm = defaultBpm,
                NotesAbove = notesAbove,
                NotesBelow = notesBelow,
                SpeedEvents = BuildSpeedEvents(chart, line, endTick),
                JudgeLineMoveEvents = BuildMoveEvents(chart, lineProperties, endTick),
                JudgeLineRotateEvents = BuildRotateEvents(chart, lineProperties, endTick),
                JudgeLineDisappearEvents = BuildDisappearEvents(chart, lineProperties, endTick)
            });
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
                .Select(note => note.HitTime + Math.Max(0, note.HoldDuration))
                .DefaultIfEmpty(0)
                .Max();
        }

        return Math.Max(1, Math.Max(metadataDuration, Math.Max(chartDuration, latestNoteTick)));
    }

    private static void BuildLineNotes(Chart chart, JudgementLine line, out List<OfficialNoteDto> notesAbove, out List<OfficialNoteDto> notesBelow)
    {
        notesAbove = new List<OfficialNoteDto>();
        notesBelow = new List<OfficialNoteDto>();

        foreach (var note in line.Notes)
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
            var officialNote = new OfficialNoteDto
            {
                Type = ConvertNoteKind(kind),
                Time = note.HitTime,
                HoldTime = kind == NoteKind.Hold ? Math.Max(0, note.HoldDuration) : 0,
                PositionX = ConvertProjectXToOfficialX(notePosition.X),
                Speed = Sanitize(note.CustomSpeed ?? 1.0),
                FloorPosition = 0
            };

            if (notePosition.Y >= 0)
            {
                notesAbove.Add(officialNote);
            }
            else
            {
                notesBelow.Add(officialNote);
            }
        }

        notesAbove.Sort((a, b) => a.Time.CompareTo(b.Time));
        notesBelow.Sort((a, b) => a.Time.CompareTo(b.Time));
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
        var breakpoints = CollectBreakpoints(endTick, speedKeyframes.Select(k => k.Time));
        var result = new List<OfficialSpeedEventDto>(breakpoints.Count - 1);

        for (int i = 0; i < breakpoints.Count - 1; i++)
        {
            int startTime = breakpoints[i];
            int endTime = breakpoints[i + 1];

            EasingUtils.CalculateObjectSingleTransform(
                startTime,
                chart.KeyFrameEasingDirection,
                line.InitialSpeed,
                speedKeyframes,
                MathUtils.Lerp,
                line.SpeedExpressionEnabled,
                line.SpeedExpressionText,
                chart,
                line,
                out double speed);

            result.Add(new OfficialSpeedEventDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Value = Sanitize(speed),
                FloorPosition = 0
            });
        }

        return result;
    }

    private static List<OfficialMoveEventDto> BuildMoveEvents(Chart chart, StandardAnimatableProperties properties, int endTick)
    {
        var moveKeyframes = properties.Offset.KeyFrames.OrderBy(k => k.Time).ToList();
        var breakpoints = CollectBreakpoints(endTick, moveKeyframes.Select(k => k.Time));
        var result = new List<OfficialMoveEventDto>(breakpoints.Count - 1);

        for (int i = 0; i < breakpoints.Count - 1; i++)
        {
            int startTime = breakpoints[i];
            int endTime = breakpoints[i + 1];

            EasingUtils.CalculateObjectSingleTransform(
                startTime,
                chart.KeyFrameEasingDirection,
                properties.Offset.InitialValue,
                moveKeyframes,
                MathUtils.Lerp,
                out Vector startValue);

            EasingUtils.CalculateObjectSingleTransform(
                endTime,
                chart.KeyFrameEasingDirection,
                properties.Offset.InitialValue,
                moveKeyframes,
                MathUtils.Lerp,
                out Vector endValue);

            result.Add(new OfficialMoveEventDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Start = NormalizeProjectXToViewport(startValue.X),
                End = NormalizeProjectXToViewport(endValue.X),
                Start2 = NormalizeProjectYToViewport(startValue.Y),
                End2 = NormalizeProjectYToViewport(endValue.Y)
            });
        }

        return result;
    }

    private static List<OfficialRotateEventDto> BuildRotateEvents(Chart chart, StandardAnimatableProperties properties, int endTick)
    {
        var rotationKeyframes = properties.Rotation.KeyFrames.OrderBy(k => k.Time).ToList();
        var breakpoints = CollectBreakpoints(endTick, rotationKeyframes.Select(k => k.Time));
        var result = new List<OfficialRotateEventDto>(breakpoints.Count - 1);

        for (int i = 0; i < breakpoints.Count - 1; i++)
        {
            int startTime = breakpoints[i];
            int endTime = breakpoints[i + 1];

            EasingUtils.CalculateObjectSingleTransform(
                startTime,
                chart.KeyFrameEasingDirection,
                properties.Rotation.InitialValue,
                rotationKeyframes,
                MathUtils.Lerp,
                out double startValue);

            EasingUtils.CalculateObjectSingleTransform(
                endTime,
                chart.KeyFrameEasingDirection,
                properties.Rotation.InitialValue,
                rotationKeyframes,
                MathUtils.Lerp,
                out double endValue);

            result.Add(new OfficialRotateEventDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Start = Sanitize(startValue),
                End = Sanitize(endValue)
            });
        }

        return result;
    }

    private static List<OfficialDisappearEventDto> BuildDisappearEvents(Chart chart, StandardAnimatableProperties properties, int endTick)
    {
        var opacityKeyframes = properties.Opacity.KeyFrames.OrderBy(k => k.Time).ToList();
        var breakpoints = CollectBreakpoints(endTick, opacityKeyframes.Select(k => k.Time));
        var result = new List<OfficialDisappearEventDto>(breakpoints.Count - 1);

        for (int i = 0; i < breakpoints.Count - 1; i++)
        {
            int startTime = breakpoints[i];
            int endTime = breakpoints[i + 1];

            EasingUtils.CalculateObjectSingleTransform(
                startTime,
                chart.KeyFrameEasingDirection,
                properties.Opacity.InitialValue,
                opacityKeyframes,
                MathUtils.Lerp,
                out double startValue);

            EasingUtils.CalculateObjectSingleTransform(
                endTime,
                chart.KeyFrameEasingDirection,
                properties.Opacity.InitialValue,
                opacityKeyframes,
                MathUtils.Lerp,
                out double endValue);

            result.Add(new OfficialDisappearEventDto
            {
                StartTime = startTime,
                EndTime = endTime,
                Start = Sanitize01(startValue / 100.0),
                End = Sanitize01(endValue / 100.0)
            });
        }

        return result;
    }

    private static List<int> CollectBreakpoints(int endTick, params IEnumerable<int>[] sequences)
    {
        var points = new HashSet<int> { 0, Math.Max(1, endTick) };

        foreach (var sequence in sequences)
        {
            foreach (var tick in sequence)
            {
                int clamped = Math.Clamp(tick, 0, Math.Max(1, endTick));
                points.Add(clamped);
            }
        }

        return points.OrderBy(t => t).ToList();
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
