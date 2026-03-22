using Axphi.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Axphi.Services;

public partial class ProjectManager : ObservableObject
{
    private const string ChartEntryName = "chart.json";
    private const string AudioEntryName = "audio";
    private const string IllustrationEntryName = "illustration";

    [ObservableProperty]
    private Project _editingProject = new Project()
    {
        Chart = new Chart()
    };

    [ObservableProperty]
    private string? _editingProjectFilePath;

    public void SaveProject(Project project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Project save path cannot be empty.", nameof(path));
        }

        using var fs = new FileStream(path, FileMode.Create);
        using ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var chartJson = JsonSerializer.Serialize(project.Chart);
        var chartJsonUtfBytes = Encoding.UTF8.GetBytes(chartJson);

        using (var chartEntry = zip.CreateEntry(ChartEntryName).Open())
        {
            chartEntry.Write(chartJsonUtfBytes);
        }

        WriteOptionalEntry(zip, AudioEntryName, project.EncodedAudio);
        WriteOptionalEntry(zip, IllustrationEntryName, project.EncodedIllustration);
    }

    public Project LoadProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Project load path cannot be empty.", nameof(path));
        }

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read);

        var chartEntry = zip.GetEntry(ChartEntryName)
            ?? throw new InvalidDataException($"Project file is missing required entry '{ChartEntryName}'.");

        Chart? chart;
        using (var chartStream = chartEntry.Open())
        using (var chartReader = new StreamReader(chartStream, Encoding.UTF8, leaveOpen: false))
        {
            var chartJson = chartReader.ReadToEnd();
            chart = JsonSerializer.Deserialize<Chart>(chartJson);
        }

        if (chart is null)
        {
            throw new InvalidDataException("Project chart data is invalid.");
        }

        return new Project
        {
            Chart = chart,
            EncodedAudio = ReadOptionalEntry(zip, AudioEntryName),
            EncodedIllustration = ReadOptionalEntry(zip, IllustrationEntryName)
        };
    }

    public void LoadEditingProject(string path)
    {
        EditingProject = LoadProject(path);
        EditingProjectFilePath = path;
    }

    private static void WriteOptionalEntry(ZipArchive zip, string entryName, byte[]? content)
    {
        if (content is not { Length: > 0 })
        {
            return;
        }

        using var entryStream = zip.CreateEntry(entryName).Open();
        entryStream.Write(content);
    }

    private static byte[]? ReadOptionalEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null || entry.Length <= 0)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        entryStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
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
