namespace Axphi.ViewModels;

public interface ITimelineMutationSyncService
{
    void SyncAfterMutation(TimelineMutationRuntime runtime);
}
