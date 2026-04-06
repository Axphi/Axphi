namespace Axphi.ViewModels;

public sealed record JudgementLineEditorUiState(
    string ActiveTrackId,
    string CurrentNoteKind,
    int HorizontalDivisions,
    double ViewZoom,
    double PanX,
    double PanY);
