using System.Collections.Generic;

namespace Axphi.ViewModels;

public sealed record TimelineUiState(
    double ViewportActualWidth,
    IReadOnlyList<TrackUiState> Tracks,
    JudgementLineEditorUiState? Editor);
