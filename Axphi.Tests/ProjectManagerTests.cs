using System.IO.Compression;
using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace Axphi.Tests;

[TestClass]
public class ProjectManagerTests
{
    [TestMethod]
    public void SaveProject_ThenLoadProject_RoundTripsChartAndAssets()
    {
        var projectManager = new ProjectManager();
        var project = new Project
        {
            Chart = new Chart
            {
                formatVersion = "2.5",
                Duration = 4096,
                SoneName = "Round Trip Song",
                Rank = Rank.IN,
                CustomRank = "SP",
                Level = 15.7,
                Composer = "Composer",
                Charter = "Charter",
                Illustrator = "Illustrator",
                InitialBpm = 180
            },
            Metadata = new ProjectMetadata
            {
                AudioOffsetTicks = 128,
                AudioVolume = 75,
                PlayheadTimeSeconds = 12.5,
                CurrentHorizontalScrollOffset = 640,
                ZoomScale = 1.75,
                TotalDurationTicks = 8192,
                WorkspaceStartTick = 96,
                WorkspaceEndTick = 2048,
                IsAudioTrackExpanded = true,
                IsAudioTrackLocked = true
            },
            EncodedAudio = [1, 2, 3, 4],
            EncodedIllustration = [5, 6, 7, 8]
        };

        string path = CreateTemporaryProjectPath();

        try
        {
            projectManager.SaveProject(project, path);

            Project loadedProject = projectManager.LoadProject(path);

            Assert.AreEqual(project.Chart.Duration, loadedProject.Chart.Duration);
            Assert.AreEqual(project.Chart.SoneName, loadedProject.Chart.SoneName);
            Assert.AreEqual(project.Chart.formatVersion, loadedProject.Chart.formatVersion);
            Assert.AreEqual(project.Chart.Rank, loadedProject.Chart.Rank);
            Assert.AreEqual(project.Chart.CustomRank, loadedProject.Chart.CustomRank);
            Assert.AreEqual(project.Chart.Level, loadedProject.Chart.Level);
            Assert.AreEqual(project.Chart.Composer, loadedProject.Chart.Composer);
            Assert.AreEqual(project.Chart.Charter, loadedProject.Chart.Charter);
            Assert.AreEqual(project.Chart.Illustrator, loadedProject.Chart.Illustrator);
            Assert.AreEqual(project.Chart.InitialBpm, loadedProject.Chart.InitialBpm);
            Assert.AreEqual(project.Metadata.AudioOffsetTicks, loadedProject.Metadata.AudioOffsetTicks);
            Assert.AreEqual(project.Metadata.AudioVolume, loadedProject.Metadata.AudioVolume);
            Assert.AreEqual(project.Metadata.PlayheadTimeSeconds, loadedProject.Metadata.PlayheadTimeSeconds);
            Assert.AreEqual(project.Metadata.CurrentHorizontalScrollOffset, loadedProject.Metadata.CurrentHorizontalScrollOffset);
            Assert.AreEqual(project.Metadata.ZoomScale, loadedProject.Metadata.ZoomScale);
            Assert.AreEqual(project.Metadata.TotalDurationTicks, loadedProject.Metadata.TotalDurationTicks);
            Assert.AreEqual(project.Metadata.WorkspaceStartTick, loadedProject.Metadata.WorkspaceStartTick);
            Assert.AreEqual(project.Metadata.WorkspaceEndTick, loadedProject.Metadata.WorkspaceEndTick);
            Assert.AreEqual(project.Metadata.IsAudioTrackExpanded, loadedProject.Metadata.IsAudioTrackExpanded);
            Assert.AreEqual(project.Metadata.IsAudioTrackLocked, loadedProject.Metadata.IsAudioTrackLocked);
            CollectionAssert.AreEqual(project.EncodedAudio, loadedProject.EncodedAudio);
            CollectionAssert.AreEqual(project.EncodedIllustration, loadedProject.EncodedIllustration);
        }
        finally
        {
            DeleteFileIfExists(path);
        }
    }

    [TestMethod]
    public void SaveProject_WithoutOptionalAssets_LoadsWithNullAssets()
    {
        var projectManager = new ProjectManager();
        var project = new Project
        {
            Chart = new Chart
            {
                SoneName = "No Assets",
                Duration = 1024
            }
        };

        string path = CreateTemporaryProjectPath();

        try
        {
            projectManager.SaveProject(project, path);

            Project loadedProject = projectManager.LoadProject(path);

            Assert.AreEqual(project.Chart.SoneName, loadedProject.Chart.SoneName);
            Assert.IsNull(loadedProject.EncodedAudio);
            Assert.IsNull(loadedProject.EncodedIllustration);
        }
        finally
        {
            DeleteFileIfExists(path);
        }
    }

    [TestMethod]
    public void LoadProject_WithoutChartEntry_ThrowsInvalidDataException()
    {
        var projectManager = new ProjectManager();
        string path = CreateTemporaryProjectPath();

        try
        {
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                using var entryStream = archive.CreateEntry("audio").Open();
                entryStream.Write([1, 2, 3]);
            }

            Assert.ThrowsExactly<InvalidDataException>(() => projectManager.LoadProject(path));
        }
        finally
        {
            DeleteFileIfExists(path);
        }
    }

    [TestMethod]
    public void LoadProject_LegacyChartMetadata_FallsBackToLegacyFields()
    {
        var projectManager = new ProjectManager();
        string path = CreateTemporaryProjectPath();

        try
        {
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                using (var chartStream = archive.CreateEntry("chart.json").Open())
                using (var writer = new StreamWriter(chartStream))
                {
                    writer.Write("{\"formatVersion\":\"1.0\",\"Duration\":2048,\"InitialBpm\":160,\"Offset\":256,\"AudioVolume\":66}");
                }
            }

            Project loadedProject = projectManager.LoadProject(path);

            Assert.AreEqual(2048, loadedProject.Chart.Duration);
            Assert.AreEqual(160d, loadedProject.Chart.InitialBpm);
            Assert.AreEqual(256, loadedProject.Metadata.AudioOffsetTicks);
            Assert.AreEqual(66d, loadedProject.Metadata.AudioVolume);
            Assert.AreEqual(2048, loadedProject.Metadata.TotalDurationTicks);
        }
        finally
        {
            DeleteFileIfExists(path);
        }
    }

    [TestMethod]
    public void SaveProject_WithEmptyPath_ThrowsArgumentException()
    {
        var projectManager = new ProjectManager();

        Assert.ThrowsExactly<ArgumentException>(() => projectManager.SaveProject(new Project(), string.Empty));
    }

    [TestMethod]
    public void LoadProject_WithEmptyPath_ThrowsArgumentException()
    {
        var projectManager = new ProjectManager();

        Assert.ThrowsExactly<ArgumentException>(() => projectManager.LoadProject(string.Empty));
    }

    [TestMethod]
    public void SaveProject_ThenLoadProject_RoundTripsReadonlyAnimatableProperties()
    {
        var projectManager = new ProjectManager();
        var project = new Project
        {
            Chart = new Chart
            {
                JudgementLines =
                [
                    new JudgementLine
                    {
                        Name = "Line A",
                        AnimatableProperties =
                        {
                            Offset =
                            {
                                InitialValue = new Vector(1.5, -2.5),
                                KeyFrames =
                                {
                                    new OffsetKeyFrame
                                    {
                                        Time = 64,
                                        Value = new Vector(-3.25, 4.5)
                                    }
                                }
                            },
                            Rotation =
                            {
                                InitialValue = 12.5
                            }
                        },
                        Notes =
                        [
                            new Note(NoteKind.Hold, 256)
                            {
                                HoldDuration = 96,
                                AnimatableProperties =
                                {
                                    Offset =
                                    {
                                        InitialValue = new Vector(2.25, 0.75),
                                        KeyFrames =
                                        {
                                            new OffsetKeyFrame
                                            {
                                                Time = 256,
                                                Value = new Vector(6.75, -1.25)
                                            }
                                        }
                                    },
                                    Scale =
                                    {
                                        InitialValue = new Vector(1.2, 0.8)
                                    }
                                }
                            }
                        ]
                    }
                ]
            }
        };

        string path = CreateTemporaryProjectPath();

        try
        {
            projectManager.SaveProject(project, path);

            Project loadedProject = projectManager.LoadProject(path);
            JudgementLine loadedLine = loadedProject.Chart.JudgementLines.Single();
            Note loadedNote = loadedLine.Notes.Single();

            Assert.AreEqual(new Vector(1.5, -2.5), loadedLine.AnimatableProperties.Offset.InitialValue);
            Assert.AreEqual(1, loadedLine.AnimatableProperties.Offset.KeyFrames.Count);
            Assert.AreEqual(new Vector(-3.25, 4.5), loadedLine.AnimatableProperties.Offset.KeyFrames[0].Value);
            Assert.AreEqual(12.5, loadedLine.AnimatableProperties.Rotation.InitialValue);

            Assert.AreEqual(new Vector(2.25, 0.75), loadedNote.AnimatableProperties.Offset.InitialValue);
            Assert.AreEqual(1, loadedNote.AnimatableProperties.Offset.KeyFrames.Count);
            Assert.AreEqual(256, loadedNote.AnimatableProperties.Offset.KeyFrames[0].Time);
            Assert.AreEqual(new Vector(6.75, -1.25), loadedNote.AnimatableProperties.Offset.KeyFrames[0].Value);
            Assert.AreEqual(new Vector(1.2, 0.8), loadedNote.AnimatableProperties.Scale.InitialValue);
        }
        finally
        {
            DeleteFileIfExists(path);
        }
    }

    private static string CreateTemporaryProjectPath()
    {
        return Path.Combine(Path.GetTempPath(), $"axphi-tests-{Guid.NewGuid():N}.axp");
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}