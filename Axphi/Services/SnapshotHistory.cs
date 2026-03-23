using System.Collections.Generic;

namespace Axphi.Services;

public sealed class SnapshotHistory<T>
{
    private readonly int _capacity;
    private readonly IEqualityComparer<T> _comparer;
    private readonly List<T> _undoSnapshots = new();
    private readonly List<T> _redoSnapshots = new();

    private T _currentSnapshot = default!;
    private T _pendingSnapshot = default!;
    private bool _isInitialized;
    private bool _hasPendingSnapshot;

    public SnapshotHistory(int capacity = 100, IEqualityComparer<T>? comparer = null)
    {
        _capacity = capacity > 0 ? capacity : 100;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public bool HasPendingChanges => _hasPendingSnapshot;

    public bool CanUndo => _undoSnapshots.Count > 0;

    public bool CanRedo => _redoSnapshots.Count > 0;

    public T CurrentSnapshot => _currentSnapshot;

    public void Reset(T snapshot)
    {
        _undoSnapshots.Clear();
        _redoSnapshots.Clear();
        _currentSnapshot = snapshot;
        _pendingSnapshot = snapshot;
        _isInitialized = true;
        _hasPendingSnapshot = false;
    }

    public void ObserveSnapshot(T snapshot)
    {
        if (!_isInitialized)
        {
            Reset(snapshot);
            return;
        }

        if (!_hasPendingSnapshot && _comparer.Equals(snapshot, _currentSnapshot))
        {
            return;
        }

        _pendingSnapshot = snapshot;
        _hasPendingSnapshot = true;
    }

    public bool FlushPendingChanges()
    {
        if (!_isInitialized || !_hasPendingSnapshot)
        {
            return false;
        }

        _hasPendingSnapshot = false;
        if (_comparer.Equals(_pendingSnapshot, _currentSnapshot))
        {
            return false;
        }

        PushUndoSnapshot(_currentSnapshot);
        _currentSnapshot = _pendingSnapshot;
        _redoSnapshots.Clear();
        return true;
    }

    public bool TryUndo(out T snapshot)
    {
        if (!CanUndo)
        {
            snapshot = _currentSnapshot;
            return false;
        }

        PushRedoSnapshot(_currentSnapshot);
        _currentSnapshot = PopSnapshot(_undoSnapshots);
        snapshot = _currentSnapshot;
        return true;
    }

    public bool TryRedo(out T snapshot)
    {
        if (!CanRedo)
        {
            snapshot = _currentSnapshot;
            return false;
        }

        PushUndoSnapshot(_currentSnapshot);
        _currentSnapshot = PopSnapshot(_redoSnapshots);
        snapshot = _currentSnapshot;
        return true;
    }

    private void PushUndoSnapshot(T snapshot)
    {
        _undoSnapshots.Add(snapshot);
        if (_undoSnapshots.Count > _capacity)
        {
            _undoSnapshots.RemoveAt(0);
        }
    }

    private void PushRedoSnapshot(T snapshot)
    {
        _redoSnapshots.Add(snapshot);
        if (_redoSnapshots.Count > _capacity)
        {
            _redoSnapshots.RemoveAt(0);
        }
    }

    private static T PopSnapshot(List<T> snapshots)
    {
        int lastIndex = snapshots.Count - 1;
        T snapshot = snapshots[lastIndex];
        snapshots.RemoveAt(lastIndex);
        return snapshot;
    }
}