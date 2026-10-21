using Axphi.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.Services;

public partial class ProjectManager : ObservableObject
{
    [ObservableProperty]
    private Project _editingProject = new()
    {
        Chart = new Chart(),
        Metadata = new ProjectMetadata()
    };

    [ObservableProperty]
    private string? _editingProjectFilePath;

    public void SetEditingProject(Project project, string? filePath)
    {
        ArgumentNullException.ThrowIfNull(project);

        EditingProject = project;
        EditingProjectFilePath = filePath;
    }
}
