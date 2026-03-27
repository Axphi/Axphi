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
}