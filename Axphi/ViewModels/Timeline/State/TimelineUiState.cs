using System.Collections.Generic;

namespace Axphi.ViewModels;

public sealed record TimelineUiState(
    double CurrentPlayTimeSeconds,
    double CurrentHorizontalScrollOffset,
    double ZoomScale,
    double ViewportActualWidth,
    int WorkspaceStartTick,
    int WorkspaceEndTick,
    bool IsAudioTrackExpanded,
    IReadOnlyList<TrackUiState> Tracks,
    JudgementLineEditorUiState? Editor);
