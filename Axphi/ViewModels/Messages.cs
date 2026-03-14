using Axphi.Data;
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

    // 告诉所有轨道：有人挪动了关键帧，请重新把底层 List 按时间排序！
    public record class KeyframesNeedSortMessage();

    // 告诉全网：我被选中了，这是我的缓动参数！
    public record class KeyframeSelectedMessage(BezierEasing Easing);

    
    // 告诉全网：请取消选中！
    // GroupName 用于区分当前是在操作谁（比如 "Keyframes" 还是 "JudgementLines"）
    // SenderToIgnore 用于保护自己（发信人自己不要被取消选中）
    public record class ClearSelectionMessage(string GroupName, object SenderToIgnore);
}
