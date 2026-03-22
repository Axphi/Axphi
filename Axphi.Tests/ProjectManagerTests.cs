using System.IO.Compression;
using Axphi.Data;
using Axphi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                Offset = 128,
                Duration = 4096,
                SoneName = "Round Trip Song",
                Rank = Rank.IN,
                CustomRank = "SP",
                Level = 15.7,
                Composer = "Composer",
                Charter = "Charter",
                Illustrator = "Illustrator",
                InitialBpm = 180,
                AudioVolume = 75
            },
            EncodedAudio = [1, 2, 3, 4],
            EncodedIllustration = [5, 6, 7, 8]
        };

        string path = CreateTemporaryProjectPath();

        try
        {
            projectManager.SaveProject(project, path);

            Project loadedProject = projectManager.LoadProject(path);

            Assert.AreEqual(project.Chart.Offset, loadedProject.Chart.Offset);
            Assert.AreEqual(project.Chart.Duration, loadedProject.Chart.Duration);
            Assert.AreEqual(project.Chart.SoneName, loadedProject.Chart.SoneName);
            Assert.AreEqual(project.Chart.Rank, loadedProject.Chart.Rank);
            Assert.AreEqual(project.Chart.CustomRank, loadedProject.Chart.CustomRank);
            Assert.AreEqual(project.Chart.Level, loadedProject.Chart.Level);
            Assert.AreEqual(project.Chart.Composer, loadedProject.Chart.Composer);
            Assert.AreEqual(project.Chart.Charter, loadedProject.Chart.Charter);
            Assert.AreEqual(project.Chart.Illustrator, loadedProject.Chart.Illustrator);
            Assert.AreEqual(project.Chart.InitialBpm, loadedProject.Chart.InitialBpm);
            Assert.AreEqual(project.Chart.AudioVolume, loadedProject.Chart.AudioVolume);
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