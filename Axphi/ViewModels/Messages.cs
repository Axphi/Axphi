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
}
