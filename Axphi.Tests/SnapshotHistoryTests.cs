using Axphi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Axphi.Tests;

[TestClass]
public class SnapshotHistoryTests
{
    [TestMethod]
    public void FlushPendingChanges_MergesContinuousUpdatesIntoSingleUndoStep()
    {
        var history = new SnapshotHistory<string>();
        history.Reset("A");

        history.ObserveSnapshot("B");
        history.ObserveSnapshot("C");
        history.FlushPendingChanges();

        Assert.IsTrue(history.CanUndo);
        Assert.AreEqual("C", history.CurrentSnapshot);

        Assert.IsTrue(history.TryUndo(out var undoSnapshot));
        Assert.AreEqual("A", undoSnapshot);
        Assert.IsTrue(history.CanRedo);

        Assert.IsTrue(history.TryRedo(out var redoSnapshot));
        Assert.AreEqual("C", redoSnapshot);
    }

    [TestMethod]
    public void FlushPendingChanges_DoesNotCreateUndoStepWhenStateReturnsToBaseline()
    {
        var history = new SnapshotHistory<string>();
        history.Reset("A");

        history.ObserveSnapshot("B");
        history.ObserveSnapshot("A");
        history.FlushPendingChanges();

        Assert.IsFalse(history.CanUndo);
        Assert.AreEqual("A", history.CurrentSnapshot);
    }
}