using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Axphi.Tests;

[TestClass]
public class BpmTrackViewModelTests
{
    [TestMethod]
    public void SyncValuesToTime_WithoutBpmKeyframes_UsesInitialBpm()
    {
        var chart = new Chart
        {
            InitialBpm = 168
        };

        var (_, bpmTrack) = CreateSubject(chart);

        bpmTrack.SyncValuesToTime(1024);

        Assert.AreEqual(168d, bpmTrack.CurrentBpm, 0.0001);
    }

    [TestMethod]
    public void CurrentBpmChanged_WithoutBpmKeyframes_UpdatesInitialBpmAndKeepsTickStable()
    {
        var chart = new Chart
        {
            InitialBpm = 120
        };

        var (timeline, bpmTrack) = CreateSubject(chart);
        timeline.CurrentPlayTimeSeconds = TimeTickConverter.TickToTime(256, chart.BpmKeyFrames, chart.InitialBpm);

        bpmTrack.CurrentBpm = 150;

        Assert.AreEqual(150d, chart.InitialBpm, 0.0001);
        Assert.AreEqual(0, chart.BpmKeyFrames.Count);
        Assert.AreEqual(256d, timeline.GetExactTick(), 0.0001);
    }

    [TestMethod]
    public void CurrentBpmChanged_WithExistingKeyframe_UpdatesCurrentTickKeyframeValue()
    {
        var chart = new Chart
        {
            InitialBpm = 120,
            BpmKeyFrames =
            [
                new KeyFrame<double> { Time = 0, Value = 120 }
            ]
        };

        var (timeline, bpmTrack) = CreateSubject(chart);
        timeline.CurrentPlayTimeSeconds = 0;
        int currentTick = timeline.GetCurrentTick();

        bpmTrack.CurrentBpm = 180;

        Assert.AreEqual(120d, chart.InitialBpm, 0.0001);
        Assert.IsTrue(chart.BpmKeyFrames.Any(k => k.Time == currentTick && Math.Abs(k.Value - 180d) < 0.0001));
    }

    private static (TimelineViewModel Timeline, BpmTrackViewModel BpmTrack) CreateSubject(Chart chart)
    {
        var projectManager = new ProjectManager
        {
            EditingProject = new Project
            {
                Chart = chart,
                Metadata = new ProjectMetadata()
            }
        };

        var timeline = new TimelineViewModel(
            projectManager,
            new TimelineTrackFactory(),
            new TimelineHistoryCoordinator(),
            new TimelineEditingService(WeakReferenceMessenger.Default),
            new TimelineSnapService(),
            new TimelineClipboardService(),
            new TimelineMutationSyncService(),
            new TimelineStateService(),
            new TimelineWorkspaceLoopService(),
            new TimelineSelectionService(),
            new TimelinePlaybackSyncService(),
            WeakReferenceMessenger.Default);
        var bpmTrack = timeline.BpmTrack ?? throw new AssertFailedException("BPM track should be initialized.");
        return (timeline, bpmTrack);
    }
}
