namespace Axphi.ViewModels
{
    public interface ITimelineDraggable
    {
        void OnDragStarted();
        void OnDragDelta(double horizontalChange);
        void OnDragCompleted();
    }

    public interface IRightClickableTimelineItem
    {
        void OnRightClick();
    }
}
