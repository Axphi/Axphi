namespace Axphi.ViewModels
{
    public interface ITimelineHistoryCoordinator
    {
        bool CanUndo { get; }

        bool CanRedo { get; }

        void ScheduleSnapshot(string snapshot);

        void FlushPending();

        void Reset(string snapshot);

        bool TryUndo(out string snapshot);

        bool TryRedo(out string snapshot);
    }
}
