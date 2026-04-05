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

    private static JsonDocument ExportOfficialChart(Project project)
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"axphi-export-{Guid.NewGuid():N}.json");

        try
        {
            Type exporterType = typeof(Project).Assembly.GetType("Axphi.Utilities.OfficialChartExporter", throwOnError: true)!;
            MethodInfo exportMethod = exporterType.GetMethod("Export", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            exportMethod.Invoke(null, [project, outputPath]);
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