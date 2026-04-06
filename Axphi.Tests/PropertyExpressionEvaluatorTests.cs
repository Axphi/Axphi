using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace Axphi.Tests;

[TestClass]
public class PropertyExpressionEvaluatorTests
{
    [TestMethod]
    public void TryEvaluateVector_UsesTickTimeAndValue()
    {
        var chart = new Chart
        {
            InitialBpm = 120,
            BpmKeyFrames =
            [
                new KeyFrame<double> { Time = 0, Value = 120 }
            ]
        };

        var context = PropertyExpressionEvaluator.CreateContext(64, chart);

        bool success = PropertyExpressionEvaluator.TryEvaluateVector(
            "[value[0] + tick, value[1] + time]",
            new Vector(1, 2),
            context,
            out var result,
            out var error);

        Assert.IsTrue(success, error);
        Assert.AreEqual(65, result.X, 0.0001);
        Assert.AreEqual(3, result.Y, 0.0001);
    }

    [TestMethod]
    public void TryEvaluateDouble_RejectsWrongResultType()
    {
        bool success = PropertyExpressionEvaluator.TryEvaluateDouble(
            "[1, 2]",
            3,
            PropertyExpressionEvaluator.CreateDesignTimeContext(),
            out var result,
            out var error);

        Assert.IsFalse(success);
        Assert.AreEqual(3, result);
        Assert.AreEqual("表达式结果必须是数字", error);
    }

    [TestMethod]
    public void TryEvaluateVector_SupportsMultilineStatementsAndLineReferences()
    {
        var sourceLine = new JudgementLine
        {
            Name = "jjj"
        };
        sourceLine.AnimatableProperties.Offset.InitialValue = new Vector(10, 20);

        var targetLine = new JudgementLine
        {
            Name = "follower"
        };

        var chart = new Chart
        {
            InitialBpm = 120,
            JudgementLines =
            [
                sourceLine,
                targetLine
            ]
        };

        var context = PropertyExpressionEvaluator.CreateContext(0, chart);

        bool success = PropertyExpressionEvaluator.TryEvaluateVector(
            "var p = line(\"jjj\").position; p + [50, 0]",
            new Vector(0, 0),
            context,
            chart,
            targetLine,
            out var result,
            out var error);

        Assert.IsTrue(success, error);
        Assert.AreEqual(60, result.X, 0.0001);
        Assert.AreEqual(20, result.Y, 0.0001);
    }

    [TestMethod]
    public void TryEvaluateVector_SupportsLineNoteAccessByIdOrName()
    {
        var note = new Note(NoteKind.Tap, 128)
        {
            Name = "main-note"
        };
        note.AnimatableProperties.Offset.InitialValue = new Vector(12, -8);

        var line = new JudgementLine
        {
            Name = "host",
            Notes =
            [
                note
            ]
        };

        var chart = new Chart
        {
            JudgementLines =
            [
                line,
                new JudgementLine { Name = "follower" }
            ]
        };

        var context = PropertyExpressionEvaluator.CreateContext(0, chart);

        bool successById = PropertyExpressionEvaluator.TryEvaluateVector(
            $"line(\"host\").notes(\"{note.ID}\").position + [8, 2]",
            new Vector(0, 0),
            context,
            chart,
            chart.JudgementLines[1],
            out var byIdResult,
            out var byIdError);

        Assert.IsTrue(successById, byIdError);
        Assert.AreEqual(20, byIdResult.X, 0.0001);
        Assert.AreEqual(-6, byIdResult.Y, 0.0001);

        bool successByName = PropertyExpressionEvaluator.TryEvaluateVector(
            "line(\"host\").notes(\"main-note\").position",
            new Vector(0, 0),
            context,
            chart,
            chart.JudgementLines[1],
            out var byNameResult,
            out var byNameError);

        Assert.IsTrue(successByName, byNameError);
        Assert.AreEqual(12, byNameResult.X, 0.0001);
        Assert.AreEqual(-8, byNameResult.Y, 0.0001);
    }

    [TestMethod]
    public void TryEvaluateDouble_LineNoteCanAccessOwningLineProperties()
    {
        var note = new Note(NoteKind.Tap, 128)
        {
            Name = "main-note"
        };

        var line = new JudgementLine
        {
            Name = "host",
            Notes =
            [
                note
            ]
        };
        line.AnimatableProperties.Offset.InitialValue = new Vector(10, 42);

        var chart = new Chart
        {
            JudgementLines =
            [
                line,
                new JudgementLine { Name = "follower" }
            ]
        };

        var context = PropertyExpressionEvaluator.CreateContext(0, chart);

        bool success = PropertyExpressionEvaluator.TryEvaluateDouble(
            "line(\"host\").notes(\"main-note\").line.position[1]",
            0,
            context,
            chart,
            chart.JudgementLines[1],
            out var result,
            out var error);

        Assert.IsTrue(success, error);
        Assert.AreEqual(42, result, 0.0001);
    }
}