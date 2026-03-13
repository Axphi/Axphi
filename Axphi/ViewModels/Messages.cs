using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.ViewModels
{

    
    internal class Messages
    {
    }
    public record AudioLoadedMessage(string FilePath);
    public record JudgementLinesChangedMessage;
    public record ProjectLoadedMessage();
    public record ZoomScaleChangedMessage(double NewZoomScale);
    public record ForcePausePlaybackMessage;

    // 告诉接收者：强制把物理时间重置为这个秒数！
    public record class ForceSeekMessage(double TargetSeconds);

    // 新增：通知所有轨道同步水平滚动的消息
    public record class SyncHorizontalScrollMessage(double Offset);
}
