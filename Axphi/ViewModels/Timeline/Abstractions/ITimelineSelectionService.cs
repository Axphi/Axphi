namespace Axphi.ViewModels;

public interface ITimelineSelectionService
{
    void ClearKeyframeSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void ClearLayerSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void ClearNoteSelection(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void ClearAllSelections(TimelineSelectionRuntime runtime);

    void EnterLayerSelectionContext(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void EnterSubItemSelectionContext(TimelineSelectionRuntime runtime, object? senderToIgnore = null);

    void RefreshLayerSelectionVisuals(TimelineSelectionRuntime runtime);
}
