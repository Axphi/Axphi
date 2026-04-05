using Axphi.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Axphi.Utilities;

namespace Axphi.Services;

public partial class ProjectManager : ObservableObject
{
    private const string ChartEntryName = "chart.json";
    private const string MetadataEntryName = "metadata.json";
    private const string AudioEntryName = "audio";
    private const string IllustrationEntryName = "illustration";
    private static readonly JsonSerializerOptions ProjectJsonSerializerOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new VectorJsonConverter() }
    };

    [ObservableProperty]
    private Project _editingProject = new Project()
    {
        Chart = new Chart(),
        Metadata = new ProjectMetadata()
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
        var chartJson = JsonSerializer.Serialize(project.Chart, ProjectJsonSerializerOptions);
        var chartJsonUtfBytes = Encoding.UTF8.GetBytes(chartJson);
        var metadataJson = JsonSerializer.Serialize(project.Metadata, ProjectJsonSerializerOptions);
        var metadataJsonUtfBytes = Encoding.UTF8.GetBytes(metadataJson);

        using (var chartEntry = zip.CreateEntry(ChartEntryName).Open())
        {
            chartEntry.Write(chartJsonUtfBytes);
        }

        using (var metadataEntry = zip.CreateEntry(MetadataEntryName).Open())
        {
            metadataEntry.Write(metadataJsonUtfBytes);
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
        string chartJson;
        using (var chartStream = chartEntry.Open())
        using (var chartReader = new StreamReader(chartStream, Encoding.UTF8, leaveOpen: false))
        {
            chartJson = chartReader.ReadToEnd();
            chart = JsonSerializer.Deserialize<Chart>(chartJson, ProjectJsonSerializerOptions);
        }

        if (chart is null)
        {
            throw new InvalidDataException("Project chart data is invalid.");
        }

        ProjectMetadata metadata = LoadMetadata(zip, chartJson);
        if (metadata.TotalDurationTicks <= 0)
        {
            metadata.TotalDurationTicks = chart.Duration > 0 ? chart.Duration : 10000;
        }

        return new Project
        {
            Chart = chart,
            Metadata = metadata,
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

    private static ProjectMetadata LoadMetadata(ZipArchive zip, string chartJson)
    {
        var metadataEntry = zip.GetEntry(MetadataEntryName);
        if (metadataEntry is not null)
        {
            using var metadataStream = metadataEntry.Open();
            using var metadataReader = new StreamReader(metadataStream, Encoding.UTF8, leaveOpen: false);
            var metadataJson = metadataReader.ReadToEnd();
            return JsonSerializer.Deserialize<ProjectMetadata>(metadataJson, ProjectJsonSerializerOptions)
                ?? new ProjectMetadata();
        }

        return CreateLegacyMetadata(chartJson);
    }

    private static ProjectMetadata CreateLegacyMetadata(string chartJson)
    {
        var metadata = new ProjectMetadata();

        if (string.IsNullOrWhiteSpace(chartJson))
        {
            return metadata;
        }

        try
        {
            using var document = JsonDocument.Parse(chartJson);
            var root = document.RootElement;

            if (root.TryGetProperty("Offset", out var offsetProperty) && offsetProperty.TryGetInt32(out int audioOffsetTicks))
            {
                metadata.AudioOffsetTicks = audioOffsetTicks;
            }

            if (root.TryGetProperty("AudioVolume", out var audioVolumeProperty) && audioVolumeProperty.TryGetDouble(out double audioVolume))
            {
                metadata.AudioVolume = audioVolume;
            }

            if (root.TryGetProperty("Duration", out var durationProperty) && durationProperty.TryGetInt32(out int durationTicks))
            {
                metadata.TotalDurationTicks = durationTicks;
            }
        }
        catch (JsonException)
        {
        }

        return metadata;
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
