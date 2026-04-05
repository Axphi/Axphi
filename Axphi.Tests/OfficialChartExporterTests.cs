using Axphi.Data;
using Axphi.Data.KeyFrames;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace Axphi.Tests;

[TestClass]
public class OfficialChartExporterTests
{
    [TestMethod]
    public void Export_BakesSpeedExpressionPerTick()
    {
        var line = new JudgementLine
        {
            SpeedExpressionEnabled = true,
            SpeedExpressionText = "tick"
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement events = document.RootElement
            .GetProperty("judgeLineList")[0]
            .GetProperty("speedEvents");

        Assert.AreEqual(4, events.GetArrayLength());
        AssertEvent(events[0], 0, 1, "value", 0);
        AssertEvent(events[1], 1, 2, "value", 1);
        AssertEvent(events[2], 2, 3, "value", 2);
        AssertEvent(events[3], 3, 4, "value", 3);
    }

    [TestMethod]
    public void Export_ComputesFloorPositionWhenEnabled()
    {
        var note = new Note(NoteKind.Tap, 64);

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            InitialSpeed = 2.0,
            Notes = [note]
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project, calculateFloorPosition: true);
        JsonElement exportedLine = document.RootElement.GetProperty("judgeLineList")[0];

        JsonElement speedEvents = exportedLine.GetProperty("speedEvents");
        Assert.AreEqual(1, speedEvents.GetArrayLength());
        Assert.AreEqual(0, speedEvents[0].GetProperty("floorPosition").GetDouble(), 0.0001);

        JsonElement notesAbove = exportedLine.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(2.0, notesAbove[0].GetProperty("floorPosition").GetDouble(), 0.0001);
    }

    [TestMethod]
    public void Export_BakesCrossLineReferencesUsingReferencedLineChanges()
    {
        var sourceLine = new JudgementLine
        {
            Name = "source"
        };
        sourceLine.AnimatableProperties.Rotation.KeyFrames.AddRange(
        [
            new RotationKeyFrame { Time = 0, Value = 0 },
            new RotationKeyFrame { Time = 2, Value = 20 },
            new RotationKeyFrame { Time = 4, Value = 20 }
        ]);

        var followerLine = new JudgementLine
        {
            Name = "follower"
        };
        followerLine.AnimatableProperties.Offset.ExpressionEnabled = true;
        followerLine.AnimatableProperties.Offset.ExpressionText = "[line(\"source\").rotation, 0]";

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                JudgementLines = [sourceLine, followerLine]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement moveEvents = document.RootElement
            .GetProperty("judgeLineList")[1]
            .GetProperty("judgeLineMoveEvents");

        Assert.AreEqual(2, moveEvents.GetArrayLength());
        AssertMoveEvent(moveEvents[0], 0, 2, 0.5, 1.75, 0.5, 0.5);
        AssertMoveEvent(moveEvents[1], 2, 4, 1.75, 1.75, 0.5, 0.5);
    }

    [TestMethod]
    public void Export_BakesBpmDrivenExpressionsAtBpmBoundaries()
    {
        var line = new JudgementLine();
        line.AnimatableProperties.Opacity.ExpressionEnabled = true;
        line.AnimatableProperties.Opacity.ExpressionText = "bpm >= 180 ? 100 : 0";

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                InitialBpm = 120,
                BpmKeyFrames =
                [
                    new KeyFrame<double> { Time = 0, Value = 120 },
                    new KeyFrame<double> { Time = 2, Value = 180 }
                ],
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement disappearEvents = document.RootElement
            .GetProperty("judgeLineList")[0]
            .GetProperty("judgeLineDisappearEvents");

        Assert.AreEqual(3, disappearEvents.GetArrayLength());
        AssertRangeEvent(disappearEvents[0], 0, 1, 0, 0);
        AssertRangeEvent(disappearEvents[1], 1, 2, 0, 1);
        AssertRangeEvent(disappearEvents[2], 2, 4, 1, 1);
    }

    [TestMethod]
    public void Export_BakesParentExpressionTransformsIntoChildEvents()
    {
        var parentLine = new JudgementLine
        {
            Name = "parent"
        };
        parentLine.AnimatableProperties.Offset.ExpressionEnabled = true;
        parentLine.AnimatableProperties.Offset.ExpressionText = "[tick, 0]";
        parentLine.AnimatableProperties.Rotation.ExpressionEnabled = true;
        parentLine.AnimatableProperties.Rotation.ExpressionText = "tick * 10";

        var childLine = new JudgementLine
        {
            Name = "child",
            ParentLineId = parentLine.ID
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                JudgementLines = [parentLine, childLine]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement child = document.RootElement.GetProperty("judgeLineList")[1];
        JsonElement moveEvents = child.GetProperty("judgeLineMoveEvents");
        JsonElement rotateEvents = child.GetProperty("judgeLineRotateEvents");

        Assert.AreEqual(1, moveEvents.GetArrayLength());
        AssertMoveEvent(moveEvents[0], 0, 4, 0.5, 0.75, 0.5, 0.5);

        Assert.AreEqual(1, rotateEvents.GetArrayLength());
        AssertRangeEvent(rotateEvents[0], 0, 4, 0, 40);
    }

    [TestMethod]
    public void Export_SplitsNoteWithXKeyframesIntoCarrierLine()
    {
        var note = new Note(NoteKind.Tap, 4);
        note.AnimatableProperties.Offset.KeyFrames.AddRange(
        [
            new OffsetKeyFrame { Time = 0, Value = new Vector(0, 0) },
            new OffsetKeyFrame { Time = 4, Value = new Vector(2, 0) }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            Notes = [note]
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesBelow").GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement carrierNote = carrier.GetProperty("notesAbove")[0];
        Assert.AreEqual(0, carrierNote.GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(1, carrierNote.GetProperty("speed").GetDouble(), 0.0001);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.AreEqual(1, moveEvents.GetArrayLength());
        AssertMoveEvent(moveEvents[0], 0, 4, 0.5, 0.625, 0.5, 0.5);

        JsonElement disappearEvents = carrier.GetProperty("judgeLineDisappearEvents");
        Assert.AreEqual(1, disappearEvents.GetArrayLength());
        AssertRangeEvent(disappearEvents[0], 0, 4, 0, 0);
    }

    [TestMethod]
    public void Export_KeepsHoldWithSingleXKeyframeInline()
    {
        var hold = new Note(NoteKind.Hold, 64)
        {
            HoldDuration = 32
        };
        hold.AnimatableProperties.Offset.KeyFrames.Add(
            new OffsetKeyFrame { Time = 64, Value = new Vector(2, 0) });

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            Notes = [hold]
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(1, lines.GetArrayLength());

        JsonElement notesAbove = lines[0].GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(2.25, notesAbove[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(1, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);
        Assert.AreEqual(32, notesAbove[0].GetProperty("holdTime").GetInt32());
    }

    [TestMethod]
    public void Export_SplitsIntegralHoldWithXMotionIntoDedicatedCarrierLine()
    {
        var hold = new Note(NoteKind.Hold, 64)
        {
            HoldDuration = 32
        };
        hold.AnimatableProperties.Offset.KeyFrames.AddRange(
        [
            new OffsetKeyFrame { Time = 0, Value = new Vector(-1, 0) },
            new OffsetKeyFrame { Time = 64, Value = new Vector(2, 0) },
            new OffsetKeyFrame { Time = 96, Value = new Vector(2, 0) }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            InitialSpeed = 1.5,
            Notes = [hold]
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement notesAbove = carrier.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(0, notesAbove[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(1.5, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);
        Assert.AreEqual(32, notesAbove[0].GetProperty("holdTime").GetInt32());

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(1, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 96, "value", 1.5);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.IsTrue(moveEvents.GetArrayLength() >= 1);
        AssertMoveEvent(moveEvents[0], 0, 64, 0.4375, 0.625, 0.5, 0.5);
    }

    [TestMethod]
    public void Export_SplitsRotatingNoteIntoFallingCarrierLine()
    {
        var note = new Note(NoteKind.Tap, 4);
        note.AnimatableProperties.Rotation.KeyFrames.AddRange(
        [
            new RotationKeyFrame { Time = 0, Value = 0 },
            new RotationKeyFrame { Time = 4, Value = 40 }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            Notes = [note]
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        JsonElement carrier = lines[1];
        JsonElement carrierNote = carrier.GetProperty("notesAbove")[0];
        Assert.AreEqual(0, carrierNote.GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(0, carrierNote.GetProperty("speed").GetDouble(), 0.0001);

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(1, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 4, "value", 0);

        JsonElement rotateEvents = carrier.GetProperty("judgeLineRotateEvents");
        Assert.AreEqual(1, rotateEvents.GetArrayLength());
        AssertRangeEvent(rotateEvents[0], 0, 4, 0, -40);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.AreEqual(1, moveEvents.GetArrayLength());
        AssertMoveEvent(moveEvents[0], 0, 4, 0.5, 0.5, 0.5375, 0.5);
    }

    [TestMethod]
    public void Export_SplitsNoteWithYKeyframesIntoFallingCarrierLine()
    {
        var note = new Note(NoteKind.Tap, 4);
        note.AnimatableProperties.Offset.KeyFrames.AddRange(
        [
            new OffsetKeyFrame { Time = 0, Value = new Vector(0, 0) },
            new OffsetKeyFrame { Time = 4, Value = new Vector(0, 1) }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            Notes = [note]
        };

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 4,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 4
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        JsonElement carrier = lines[1];
        JsonElement carrierNote = carrier.GetProperty("notesAbove")[0];
        Assert.AreEqual(0, carrierNote.GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(0, carrierNote.GetProperty("speed").GetDouble(), 0.0001);

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(1, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 4, "value", 0);

        JsonElement rotateEvents = carrier.GetProperty("judgeLineRotateEvents");
        Assert.AreEqual(1, rotateEvents.GetArrayLength());
        AssertRangeEvent(rotateEvents[0], 0, 4, 0, 0);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.AreEqual(1, moveEvents.GetArrayLength());
        AssertMoveEvent(moveEvents[0], 0, 4, 0.5, 0.5, 0.5375, 0.3888888889);
    }

    [TestMethod]
    public void Export_BindsRealtimeLineNotesToSharedCarrierLinePerTick()
    {
        var leftNote = new Note(NoteKind.Tap, 64);
        leftNote.AnimatableProperties.Offset.InitialValue = new Vector(-1, 0);

        var rightNote = new Note(NoteKind.Flick, 64);
        rightNote.AnimatableProperties.Offset.InitialValue = new Vector(2, 0);

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            Notes = [leftNote, rightNote]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.0 },
            new KeyFrame<double> { Time = 48, Value = 2.0 },
            new KeyFrame<double> { Time = 64, Value = 2.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 64,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 64
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesBelow").GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement carrierNotes = carrier.GetProperty("notesAbove");
        Assert.AreEqual(2, carrierNotes.GetArrayLength());
        Assert.AreEqual(-1.125, carrierNotes[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(0, carrierNotes[0].GetProperty("speed").GetDouble(), 0.0001);
        Assert.AreEqual(2.25, carrierNotes[1].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(0, carrierNotes[1].GetProperty("speed").GetDouble(), 0.0001);

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(1, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 64, "value", 0);

        JsonElement rotateEvents = carrier.GetProperty("judgeLineRotateEvents");
        Assert.AreEqual(1, rotateEvents.GetArrayLength());
        AssertRangeEvent(rotateEvents[0], 0, 64, 0, 0);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.IsTrue(moveEvents.GetArrayLength() >= 2);
        Assert.AreEqual(0, moveEvents[0].GetProperty("startTime").GetInt32());
        Assert.AreEqual(64, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("endTime").GetInt32());
        Assert.AreEqual(0.5, moveEvents[0].GetProperty("start").GetDouble(), 0.0001);
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("end").GetDouble(), 0.0001);
        Assert.IsTrue(moveEvents[0].GetProperty("start2").GetDouble() > 0.5);
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("end2").GetDouble(), 0.0001);

        JsonElement disappearEvents = carrier.GetProperty("judgeLineDisappearEvents");
        Assert.AreEqual(1, disappearEvents.GetArrayLength());
        AssertRangeEvent(disappearEvents[0], 0, 64, 0, 0);
    }

    [TestMethod]
    public void Export_BoundRealtimeHoldUsesOriginalLineSpeedAfterHit()
    {
        var hold = new Note(NoteKind.Hold, 64)
        {
            HoldDuration = 32
        };

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            InitialSpeed = 1.0,
            Notes = [hold]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.0 },
            new KeyFrame<double> { Time = 48, Value = 2.0 },
            new KeyFrame<double> { Time = 80, Value = 2.0 },
            new KeyFrame<double> { Time = 81, Value = 3.0 },
            new KeyFrame<double> { Time = 96, Value = 3.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement notesAbove = carrier.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(32, notesAbove[0].GetProperty("holdTime").GetInt32());
        Assert.AreEqual(2, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(3, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 64, "value", 0);
        AssertEvent(speedEvents[1], 64, 81, "value", 2);
        AssertEvent(speedEvents[2], 81, 96, "value", 3);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.IsTrue(moveEvents.GetArrayLength() >= 2);
        Assert.AreEqual(96, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("endTime").GetInt32());
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("start").GetDouble(), 0.0001);
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("end").GetDouble(), 0.0001);
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("start2").GetDouble(), 0.0001);
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("end2").GetDouble(), 0.0001);
    }

    [TestMethod]
    public void Export_BindsRealtimeHoldWithXMotionIntoDedicatedCarrierLine()
    {
        var hold = new Note(NoteKind.Hold, 64)
        {
            HoldDuration = 32
        };
        hold.AnimatableProperties.Offset.KeyFrames.AddRange(
        [
            new OffsetKeyFrame { Time = 0, Value = new Vector(-1, 0) },
            new OffsetKeyFrame { Time = 64, Value = new Vector(2, 0) },
            new OffsetKeyFrame { Time = 96, Value = new Vector(2, 0) }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            InitialSpeed = 1.0,
            Notes = [hold]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.0 },
            new KeyFrame<double> { Time = 48, Value = 2.0 },
            new KeyFrame<double> { Time = 80, Value = 2.0 },
            new KeyFrame<double> { Time = 81, Value = 3.0 },
            new KeyFrame<double> { Time = 96, Value = 3.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesBelow").GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement notesAbove = carrier.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(0, notesAbove[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(2, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);
        Assert.AreEqual(32, notesAbove[0].GetProperty("holdTime").GetInt32());

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(3, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 64, "value", 0);
        AssertEvent(speedEvents[1], 64, 81, "value", 2);
        AssertEvent(speedEvents[2], 81, 96, "value", 3);

        JsonElement moveEvents = carrier.GetProperty("judgeLineMoveEvents");
        Assert.IsTrue(moveEvents.GetArrayLength() >= 2);
        Assert.AreEqual(0.625, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("end").GetDouble(), 0.0001);
        Assert.AreEqual(0.5, moveEvents[moveEvents.GetArrayLength() - 1].GetProperty("end2").GetDouble(), 0.0001);
    }

    [TestMethod]
    public void Export_BindsRealtimeHoldWithSingleXKeyframeToSharedCarrierLine()
    {
        var hold = new Note(NoteKind.Hold, 64)
        {
            HoldDuration = 32
        };
        hold.AnimatableProperties.Offset.KeyFrames.Add(
            new OffsetKeyFrame { Time = 64, Value = new Vector(2, 0) });

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            InitialSpeed = 1.0,
            Notes = [hold]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.0 },
            new KeyFrame<double> { Time = 48, Value = 2.0 },
            new KeyFrame<double> { Time = 80, Value = 2.0 },
            new KeyFrame<double> { Time = 81, Value = 3.0 },
            new KeyFrame<double> { Time = 96, Value = 3.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement notesAbove = carrier.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(2.25, notesAbove[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(2, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);
        Assert.AreEqual(32, notesAbove[0].GetProperty("holdTime").GetInt32());

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(3, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 64, "value", 0);
        AssertEvent(speedEvents[1], 64, 81, "value", 2);
        AssertEvent(speedEvents[2], 81, 96, "value", 3);
    }

    [TestMethod]
    public void Export_FullCarrierHoldRestoresOriginalSpeedAfterHit()
    {
        var hold = new Note(NoteKind.Hold, 64)
        {
            HoldDuration = 32
        };
        hold.AnimatableProperties.Rotation.KeyFrames.AddRange(
        [
            new RotationKeyFrame { Time = 0, Value = 0 },
            new RotationKeyFrame { Time = 64, Value = 45 },
            new RotationKeyFrame { Time = 96, Value = 45 }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Integral",
            InitialSpeed = 1.5,
            Notes = [hold]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.5 },
            new KeyFrame<double> { Time = 80, Value = 1.5 },
            new KeyFrame<double> { Time = 81, Value = 2.5 },
            new KeyFrame<double> { Time = 96, Value = 2.5 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement carrier = document.RootElement.GetProperty("judgeLineList")[1];
        JsonElement notesAbove = carrier.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(1.5, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);

        JsonElement speedEvents = carrier.GetProperty("speedEvents");
        Assert.AreEqual(3, speedEvents.GetArrayLength());
        AssertEvent(speedEvents[0], 0, 64, "value", 0);
        AssertEvent(speedEvents[1], 64, 81, "value", 1.5);
        AssertEvent(speedEvents[2], 81, 96, "value", 2.5);
    }

    [TestMethod]
    public void Export_RealtimeMixedNotesShareOnlyUnaffectedCarrier()
    {
        var sharedTap = new Note(NoteKind.Tap, 64);
        sharedTap.AnimatableProperties.Offset.InitialValue = new Vector(-1, 0);

        var xMotionTap = new Note(NoteKind.Tap, 64);
        xMotionTap.AnimatableProperties.Offset.KeyFrames.AddRange(
        [
            new OffsetKeyFrame { Time = 0, Value = new Vector(0, 0) },
            new OffsetKeyFrame { Time = 64, Value = new Vector(2, 0) },
            new OffsetKeyFrame { Time = 96, Value = new Vector(2, 0) }
        ]);

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            Notes = [sharedTap, xMotionTap]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.0 },
            new KeyFrame<double> { Time = 48, Value = 2.0 },
            new KeyFrame<double> { Time = 64, Value = 2.0 },
            new KeyFrame<double> { Time = 96, Value = 2.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 96,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 96
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");
        Assert.AreEqual(3, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());

        JsonElement dedicatedCarrier = lines[1];
        JsonElement dedicatedNotes = dedicatedCarrier.GetProperty("notesAbove");
        Assert.AreEqual(1, dedicatedNotes.GetArrayLength());
        Assert.AreEqual(0, dedicatedNotes[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(0, dedicatedNotes[0].GetProperty("speed").GetDouble(), 0.0001);

        JsonElement sharedCarrier = lines[2];
        JsonElement sharedNotes = sharedCarrier.GetProperty("notesAbove");
        Assert.AreEqual(1, sharedNotes.GetArrayLength());
        Assert.AreEqual(-1.125, sharedNotes[0].GetProperty("positionX").GetDouble(), 0.0001);
    }

    [TestMethod]
    public void Export_DoesNotBindRealtimeLineNotesWhenVisibleWindowHasNoSpeedChange()
    {
        var note = new Note(NoteKind.Tap, 64);
        note.AnimatableProperties.Offset.InitialValue = new Vector(1, 0);

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            Notes = [note]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 2.0 },
            new KeyFrame<double> { Time = 16, Value = 2.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 64,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 64
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(1, lines.GetArrayLength());
        JsonElement originalLine = lines[0];
        JsonElement notesAbove = originalLine.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(1.125, notesAbove[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(1, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);
    }

    [TestMethod]
    public void Export_BindsRealtimeNoteWhenOnlySlightlyVisibleInsideExpandedBuffer()
    {
        var note = new Note(NoteKind.Tap, 64);
        note.AnimatableProperties.Offset.InitialValue = new Vector(8.95, 0);

        var line = new JudgementLine
        {
            SpeedMode = "Realtime",
            Notes = [note]
        };
        line.SpeedKeyFrames.AddRange(
        [
            new KeyFrame<double> { Time = 0, Value = 1.0 },
            new KeyFrame<double> { Time = 63, Value = 2.0 },
            new KeyFrame<double> { Time = 64, Value = 2.0 }
        ]);

        var project = new Project
        {
            Chart = new Chart
            {
                Duration = 64,
                JudgementLines = [line]
            },
            Metadata = new ProjectMetadata
            {
                TotalDurationTicks = 64
            }
        };

        using JsonDocument document = ExportOfficialChart(project);
        JsonElement lines = document.RootElement.GetProperty("judgeLineList");

        Assert.AreEqual(2, lines.GetArrayLength());
        Assert.AreEqual(0, lines[0].GetProperty("notesAbove").GetArrayLength());

        JsonElement carrier = lines[1];
        JsonElement notesAbove = carrier.GetProperty("notesAbove");
        Assert.AreEqual(1, notesAbove.GetArrayLength());
        Assert.AreEqual(10.06875, notesAbove[0].GetProperty("positionX").GetDouble(), 0.0001);
        Assert.AreEqual(0, notesAbove[0].GetProperty("speed").GetDouble(), 0.0001);
    }

    private static JsonDocument ExportOfficialChart(Project project, bool calculateFloorPosition = false)
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"axphi-export-{Guid.NewGuid():N}.json");

        try
        {
            Type exporterType = typeof(Project).Assembly.GetType("Axphi.Utilities.OfficialChartExporter", throwOnError: true)!;
            if (!calculateFloorPosition)
            {
                MethodInfo exportMethod = exporterType.GetMethod("Export", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
                exportMethod.Invoke(null, [project, outputPath]);
            }
            else
            {
                MethodInfo exportMethod = exporterType.GetMethod("ExportWithProgress", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
                Type optionsType = typeof(Project).Assembly.GetType("Axphi.Utilities.OfficialChartExporter+ExportOptions", throwOnError: true)!;
                object options = Activator.CreateInstance(optionsType, [true])!;
                exportMethod.Invoke(null, [project, outputPath, null, options]);
            }

            return JsonDocument.Parse(File.ReadAllText(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static void AssertEvent(JsonElement element, int expectedStart, int expectedEnd, string valueProperty, double expectedValue)
    {
        Assert.AreEqual(expectedStart, element.GetProperty("startTime").GetInt32());
        Assert.AreEqual(expectedEnd, element.GetProperty("endTime").GetInt32());
        Assert.AreEqual(expectedValue, element.GetProperty(valueProperty).GetDouble(), 0.0001);
    }

    private static void AssertRangeEvent(JsonElement element, int expectedStart, int expectedEnd, double expectedStartValue, double expectedEndValue)
    {
        Assert.AreEqual(expectedStart, element.GetProperty("startTime").GetInt32());
        Assert.AreEqual(expectedEnd, element.GetProperty("endTime").GetInt32());
        Assert.AreEqual(expectedStartValue, element.GetProperty("start").GetDouble(), 0.0001);
        Assert.AreEqual(expectedEndValue, element.GetProperty("end").GetDouble(), 0.0001);
    }

    private static void AssertMoveEvent(JsonElement element, int expectedStart, int expectedEnd, double expectedStartX, double expectedEndX, double expectedStartY, double expectedEndY)
    {
        Assert.AreEqual(expectedStart, element.GetProperty("startTime").GetInt32());
        Assert.AreEqual(expectedEnd, element.GetProperty("endTime").GetInt32());
        Assert.AreEqual(expectedStartX, element.GetProperty("start").GetDouble(), 0.0001);
        Assert.AreEqual(expectedEndX, element.GetProperty("end").GetDouble(), 0.0001);
        Assert.AreEqual(expectedStartY, element.GetProperty("start2").GetDouble(), 0.0001);
        Assert.AreEqual(expectedEndY, element.GetProperty("end2").GetDouble(), 0.0001);
    }
}