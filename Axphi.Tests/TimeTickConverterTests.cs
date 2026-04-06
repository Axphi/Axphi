using Axphi.Data.KeyFrames;
using Axphi.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Axphi.Tests;

[TestClass]
public class TimeTickConverterTests
{
    [TestMethod]
    public void TickToTime_WithFirstBpmKeyframeAfterZero_UsesFirstKeyframeValueBeforeFirstKeyframe()
    {
        var bpmKeyframes = new List<KeyFrame<double>>
        {
            new KeyFrame<double> { Time = 128, Value = 240 }
        };

        // 幽灵关键帧语义：首帧前区间也使用首帧值（240 BPM）
        double seconds = TimeTickConverter.TickToTime(64, bpmKeyframes, defaultBpm: 120);

        Assert.AreEqual(0.5d, seconds, 0.0001d);
    }

    [TestMethod]
    public void TimeToTick_WithFirstBpmKeyframeAfterZero_UsesFirstKeyframeValueBeforeFirstKeyframe()
    {
        var bpmKeyframes = new List<KeyFrame<double>>
        {
            new KeyFrame<double> { Time = 128, Value = 240 }
        };

        // 幽灵关键帧语义：首帧前区间也使用首帧值（240 BPM）
        double tick = TimeTickConverter.TimeToTick(0.5d, bpmKeyframes, defaultBpm: 120);

        Assert.AreEqual(64d, tick, 0.0001d);
    }

    [TestMethod]
    public void TimeToTick_And_TickToTime_AreConsistent_InGhostSegment()
    {
        var bpmKeyframes = new List<KeyFrame<double>>
        {
            new KeyFrame<double> { Time = 128, Value = 240 }
        };

        const double sourceTick = 96d;
        double seconds = TimeTickConverter.TickToTime(sourceTick, bpmKeyframes, defaultBpm: 120);
        double roundTripTick = TimeTickConverter.TimeToTick(seconds, bpmKeyframes, defaultBpm: 120);

        Assert.AreEqual(sourceTick, roundTripTick, 0.0001d);
    }
}
