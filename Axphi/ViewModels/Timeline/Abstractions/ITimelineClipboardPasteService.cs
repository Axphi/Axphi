namespace Axphi.ViewModels;

public interface ITimelineClipboardPasteService
{
    object? PasteClipboardItem(TimelinePasteRuntime runtime, KeyframeClipboardItem item, int targetTime);
}
