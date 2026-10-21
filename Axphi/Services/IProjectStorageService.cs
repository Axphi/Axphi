using Axphi.Data;

namespace Axphi.Services;

public interface IProjectStorageService
{
    Project LoadProject(string path);

    void SaveProject(Project project, string path);
}
