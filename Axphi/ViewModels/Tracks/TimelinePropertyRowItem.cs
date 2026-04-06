using System.Collections;

namespace Axphi.ViewModels;

public sealed class TimelinePropertyRowItem
{
    public TimelinePropertyRowItem(
        IEnumerable keyframesSource,
        string keyframeToolTip,
        TrackExpressionSlot? expressionSlot = null,
        string keyframeFill = "#FFD700",
        string? rowBackground = null,
        string? rowBorderBrush = null,
        bool showExpressionEditor = true,
        bool enableRightClick = true)
    {
        KeyframesSource = keyframesSource;
        KeyframeToolTip = keyframeToolTip;
        ExpressionSlot = expressionSlot;
        KeyframeFill = keyframeFill;
        RowBackground = rowBackground;
        RowBorderBrush = rowBorderBrush;
        ShowExpressionEditor = showExpressionEditor;
        EnableRightClick = enableRightClick;
    }

    public IEnumerable KeyframesSource { get; }

    public string KeyframeToolTip { get; }

    public TrackExpressionSlot? ExpressionSlot { get; }

    public string KeyframeFill { get; }

    public string? RowBackground { get; }

    public string? RowBorderBrush { get; }

    public bool ShowExpressionEditor { get; }

    public bool EnableRightClick { get; }
}
