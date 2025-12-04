using Axphi.Data;
using Axphi.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Text;
using System.Text.Json;

namespace Axphi.Services;

public partial class ProjectManager : ObservableObject
{
    [ObservableProperty]
    private Project _editingProject = new Project()
    {
        Chart = new Chart()
    };

    [ObservableProperty]
    private string? _editingProjectFilePath;

    public void SaveProject(Project project, string path)
    {
        using var fs = new FileStream(path, FileMode.Create);
        using ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var chartJson = JsonSerializer.Serialize(project.Chart);
        var chartJsonUtfBytes = Encoding.UTF8.GetBytes(chartJson);

        var chartEntry = zip.CreateEntry("chart.json").Open();
        chartEntry.Write(chartJsonUtfBytes);
        chartEntry.Close();
        var audioEntry = zip.CreateEntry("audio").Open();
        audioEntry.Write(project.EncodedAudio);
        audioEntry.Close();
        var illustrationEntry = zip.CreateEntry("illustration").Open();
        illustrationEntry.Write(project.EncodedIllustration);
        illustrationEntry.Close();
    }

    public void SaveEditingProject(string path)
    {
        if (EditingProject is null)
        {
            throw new InvalidOperationException("No project editing");
        }
        SaveProject(EditingProject, path);
    }
}
