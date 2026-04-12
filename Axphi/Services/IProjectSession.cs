using Axphi.Data;

namespace Axphi.Services;

public interface IProjectSession
{
    Project EditingProject { get; set; }
    string? EditingProjectFilePath { get; set; }
}
