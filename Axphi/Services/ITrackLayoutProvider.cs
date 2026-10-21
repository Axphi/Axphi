using Axphi.Data;

namespace Axphi.Services;

public interface ITrackLayoutProvider
{
    double BaseTrackHeight { get; }

    double PropertyRowHeight { get; }

    double TrackSpacing { get; }

    double ContentHeight { get; }

    double GetLineYCoordinate(JudgementLine line);

    double GetPropertyYCoordinate(JudgementLine line, string propertyName);

    IReadOnlyList<string> GetActiveProperties(JudgementLine line);

    bool IsLineExpanded(JudgementLine line);

    void ToggleLineExpanded(JudgementLine line);
}
