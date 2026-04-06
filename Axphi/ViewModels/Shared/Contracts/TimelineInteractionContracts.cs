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

    public interface ILayerPointerInteractable
    {
        void HandleLayerPointerDown();
        void HandleLayerPointerUp();
    }

    public interface ILayerResizable
    {
        void BeginResizeLeft();
        void ResizeLeft(double horizontalChange);
        void EndResizeLeft();
        void BeginResizeRight();
        void ResizeRight(double horizontalChange);
        void EndResizeRight();
    }
}
