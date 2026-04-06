using Axphi.Data;
using Axphi.Utilities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axphi.ViewModels;

public sealed class TimelineSnapshotService : ITimelineSnapshotService
{
    private sealed record SnapshotDocument(Chart Chart, ProjectMetadata Metadata);

    private static readonly JsonSerializerOptions SnapshotJsonSerializerOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new VectorJsonConverter() }
    };

    public string Serialize(Chart chart, ProjectMetadata metadata)
    {
        return JsonSerializer.Serialize(
            new SnapshotDocument(chart, CloneMetadata(metadata)),
            SnapshotJsonSerializerOptions);
    }

    public (Chart Chart, ProjectMetadata Metadata) Deserialize(string snapshot)
    {
        var document = JsonSerializer.Deserialize<SnapshotDocument>(snapshot, SnapshotJsonSerializerOptions)
            ?? new SnapshotDocument(new Chart(), new ProjectMetadata());

        return (document.Chart, document.Metadata);
    }

    public ProjectMetadata CloneMetadata(ProjectMetadata metadata)
    {
        return new ProjectMetadata
        {
            AudioOffsetTicks = metadata.AudioOffsetTicks,
            AudioVolume = metadata.AudioVolume,
            PlayheadTimeSeconds = metadata.PlayheadTimeSeconds,
            CurrentHorizontalScrollOffset = metadata.CurrentHorizontalScrollOffset,
            ZoomScale = metadata.ZoomScale,
            TotalDurationTicks = metadata.TotalDurationTicks,
            WorkspaceStartTick = metadata.WorkspaceStartTick,
            WorkspaceEndTick = metadata.WorkspaceEndTick,
            IsAudioTrackExpanded = metadata.IsAudioTrackExpanded,
            IsAudioTrackLocked = metadata.IsAudioTrackLocked,
            PlaybackSpeed = metadata.PlaybackSpeed,
            BackgroundDimOpacity = metadata.BackgroundDimOpacity,
            PreserveAudioPitch = metadata.PreserveAudioPitch
        };
    }
}
