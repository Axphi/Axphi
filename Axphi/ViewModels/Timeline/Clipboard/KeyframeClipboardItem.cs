using Axphi.Data;

namespace Axphi.ViewModels;

public sealed record KeyframeClipboardItem(
    KeyframeClipboardTarget Target,
    object? Owner,
    int Time,
    object Value,
    BezierEasing Easing,
    bool IsFreezeKeyframe);
