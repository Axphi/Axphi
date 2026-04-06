using Axphi.Data;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;

namespace Axphi.ViewModels;

public partial class TimelineViewModel
{
    public void NotifyKeyframeClipboardCommandsStateChanged()
    {
        CopySelectedKeyframesCommand.NotifyCanExecuteChanged();
        PasteCopiedKeyframesCommand.NotifyCanExecuteChanged();
        DuplicateSelectedLayersCommand.NotifyCanExecuteChanged();
    }

    private List<TrackViewModel> GetSelectedJudgementLineTracks()
    {
        return _timelineDomain.Clipboard.GetSelectedJudgementLineTracks(Tracks);
    }

    private int GetSelectedKeyframeCount()
    {
        return _timelineDomain.Clipboard.GetSelectedKeyframeCount(BpmTrack, Tracks);
    }

    private bool CanCopySelectedKeyframes() => GetSelectedKeyframeCount() > 0 || GetSelectedJudgementLineTracks().Count > 0;

    private bool CanPasteCopiedKeyframes() => _keyframeClipboard.Count > 0 || _judgementLineClipboard.Count > 0;

    private bool CanDuplicateSelectedLayers() => GetSelectedJudgementLineTracks().Count > 0;

    [RelayCommand(CanExecute = nameof(CanDuplicateSelectedLayers))]
    private void DuplicateSelectedLayers()
    {
        var selectedTracks = GetSelectedJudgementLineTracks();
        if (selectedTracks.Count == 0)
        {
            return;
        }

        _keyframeClipboard.Clear();
        _judgementLineClipboard.Clear();
        _judgementLineClipboard.AddRange(_timelineDomain.Clipboard.CloneJudgementLinesWithMappedParents(selectedTracks.Select(track => track.Data)));

        PasteCopiedJudgementLines();
        NotifyKeyframeClipboardCommandsStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCopySelectedKeyframes))]
    private void CopySelectedKeyframes()
    {
        if (ActiveSelectionContext == TimelineSelectionContext.Layers)
        {
            var selectedTracks = GetSelectedJudgementLineTracks();
            if (selectedTracks.Count > 0)
            {
                _keyframeClipboard.Clear();
                _judgementLineClipboard.Clear();
                _judgementLineClipboard.AddRange(_timelineDomain.Clipboard.CloneJudgementLinesWithMappedParents(selectedTracks.Select(track => track.Data)));

                NotifyKeyframeClipboardCommandsStateChanged();
                return;
            }
        }

        _judgementLineClipboard.Clear();
        _keyframeClipboard.Clear();
        var copiedKeys = new HashSet<string>();

        if (BpmTrack != null)
        {
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, BpmTrack.UIBpmKeyframes, KeyframeClipboardTarget.Bpm, null, copiedKeys);
        }

        foreach (var track in Tracks)
        {
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, track.UIAnchorKeyframes, KeyframeClipboardTarget.TrackAnchor, track, copiedKeys);
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, track.UIOffsetKeyframes, KeyframeClipboardTarget.TrackOffset, track, copiedKeys);
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, track.UIScaleKeyframes, KeyframeClipboardTarget.TrackScale, track, copiedKeys);
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, track.UIRotationKeyframes, KeyframeClipboardTarget.TrackRotation, track, copiedKeys);
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, track.UIOpacityKeyframes, KeyframeClipboardTarget.TrackOpacity, track, copiedKeys);
            _timelineDomain.Clipboard.AddSelectedWrappersToClipboard(_keyframeClipboard, track.UISpeedKeyframes, KeyframeClipboardTarget.TrackSpeed, track, copiedKeys);

            foreach (var note in track.UINotes)
            {
                _timelineDomain.Clipboard.AddNoteSelectionToClipboard(_keyframeClipboard, note, track, copiedKeys);
            }
        }

        NotifyKeyframeClipboardCommandsStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPasteCopiedKeyframes))]
    private void PasteCopiedKeyframes()
    {
        if (_judgementLineClipboard.Count > 0)
        {
            PasteCopiedJudgementLines();
            return;
        }

        if (_keyframeClipboard.Count == 0)
        {
            return;
        }

        int earliestTime = _keyframeClipboard.Min(item => item.Time);
        int cursorTick = GetCurrentTick();
        int deltaTick = cursorTick - earliestTime;
        bool containsNoteBodies = _keyframeClipboard.Any(item => item.Target == KeyframeClipboardTarget.NoteBody);

        EnterSubItemSelectionContext();
        ClearKeyframeSelection();
        if (containsNoteBodies)
        {
            ClearNoteSelection();
        }

        var pastedWrappers = new List<object>();
        var pasteRuntime = CreatePasteRuntime();
        foreach (var item in _keyframeClipboard.OrderBy(item => item.Time))
        {
            object? pastedWrapper = _timelineDomain.Clipboard.PasteClipboardItem(pasteRuntime, item, item.Time + deltaTick);
            if (pastedWrapper != null)
            {
                pastedWrappers.Add(pastedWrapper);
            }
        }

        _timelineDomain.Clipboard.ApplySelectionToPastedItems(pastedWrappers);

        RefreshLayerSelectionVisuals();
        NotifyKeyframeClipboardCommandsStateChanged();
        _timelineDomain.MutationSync.SyncAfterMutation(CreateMutationRuntime(syncNotes: true, broadcastSortMessage: true));
    }

    private void PasteCopiedJudgementLines()
    {
        if (_judgementLineClipboard.Count == 0)
        {
            return;
        }

        EnterLayerSelectionContext();
        ClearLayerSelection();

        var clonedLines = _timelineDomain.Clipboard.CloneJudgementLinesWithMappedParents(_judgementLineClipboard);

        foreach (var clonedLine in clonedLines)
        {
            CurrentChart.JudgementLines.Add(clonedLine);

            var newTrackVM = _trackFactory.CreateTrack(clonedLine, $"判定线图层 {Tracks.Count + 1}", this);
            newTrackVM.IsLayerSelected = true;
            Tracks.Add(newTrackVM);
        }

        ActiveSelectionContext = TimelineSelectionContext.Layers;
        ReindexTrackNames();
        RefreshParentLineBindings();
        RefreshLayerSelectionVisuals();
        NotifyKeyframeClipboardCommandsStateChanged();
        _timelineDomain.MutationSync.SyncAfterMutation(CreateMutationRuntime(syncNotes: false, broadcastSortMessage: false));
    }

    private TimelinePasteRuntime CreatePasteRuntime()
    {
        return new TimelinePasteRuntime(
            CurrentChart,
            BpmTrack,
            Tracks,
            this,
            _timelineDomain.Clipboard);
    }
}
