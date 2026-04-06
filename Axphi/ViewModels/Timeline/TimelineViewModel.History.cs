using CommunityToolkit.Mvvm.Input;
using System;

namespace Axphi.ViewModels;

public partial class TimelineViewModel
{
    private bool CanUndo() => _historyCoordinator.CanUndo;

    private bool CanRedo() => _historyCoordinator.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!_historyCoordinator.TryUndo(out var snapshot))
        {
            return;
        }

        ApplyHistorySnapshot(snapshot);
        NotifyHistoryCommandsStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!_historyCoordinator.TryRedo(out var snapshot))
        {
            return;
        }

        ApplyHistorySnapshot(snapshot);
        NotifyHistoryCommandsStateChanged();
    }

    private void ScheduleHistorySnapshotCapture()
    {
        if (_isReloadingChartState || _isApplyingHistorySnapshot || _projectSession.EditingProject?.Chart == null)
        {
            return;
        }

        _historyCoordinator.ScheduleSnapshot(SerializeHistorySnapshot());
        NotifyHistoryCommandsStateChanged();
    }

    private void FlushPendingHistorySnapshot()
    {
        _historyCoordinator.FlushPending();
        NotifyHistoryCommandsStateChanged();
    }

    private void ResetHistorySnapshot()
    {
        _historyCoordinator.Reset(SerializeHistorySnapshot());
        NotifyHistoryCommandsStateChanged();
    }

    private string SerializeHistorySnapshot()
    {
        return _timelineDomain.State.SerializeSnapshot(CurrentChart, GetProjectMetadata());
    }

    private TimelineUiState CaptureTimelineUiState()
    {
        return _timelineDomain.State.CaptureUiState(new TimelineCaptureRuntime(
            CurrentPlayTimeSeconds,
            CurrentHorizontalScrollOffset,
            ZoomScale,
            ViewportActualWidth,
            WorkspaceStartTick,
            WorkspaceEndTick,
            AudioTrack?.IsExpanded ?? false,
            Tracks,
            JudgementLineEditor));
    }

    private void ApplyHistorySnapshot(string snapshot)
    {
        if (_projectSession.EditingProject == null)
        {
            return;
        }

        var uiState = CaptureTimelineUiState();
        var currentProject = _projectSession.EditingProject;

        _isApplyingHistorySnapshot = true;
        try
        {
            _projectSession.EditingProject = _timelineDomain.State.RestoreProjectFromSnapshot(snapshot, currentProject);

            ReloadTracksFromCurrentChart(uiState);
        }
        finally
        {
            _isApplyingHistorySnapshot = false;
        }
    }

    private void NotifyHistoryCommandsStateChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }
}
