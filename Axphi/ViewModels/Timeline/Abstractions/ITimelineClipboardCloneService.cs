using Axphi.Data;

namespace Axphi.ViewModels;

public interface ITimelineClipboardCloneService
{
    Note CloneNote(Note note);

    JudgementLine CloneJudgementLine(JudgementLine line);
}
