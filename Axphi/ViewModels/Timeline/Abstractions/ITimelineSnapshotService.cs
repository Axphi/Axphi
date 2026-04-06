using Axphi.Data;

namespace Axphi.ViewModels;

public interface ITimelineSnapshotService
{
    string Serialize(Chart chart, ProjectMetadata metadata);

    (Chart Chart, ProjectMetadata Metadata) Deserialize(string snapshot);

    ProjectMetadata CloneMetadata(ProjectMetadata metadata);
}
