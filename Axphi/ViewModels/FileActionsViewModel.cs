using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

using CommunityToolkit.Mvvm.Messaging;
using Axphi.Services;
using Axphi.Utilities;
using Axphi.Views.Dialogs;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Threading.Tasks;

namespace Axphi.ViewModels;

public partial class FileActionsViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    public ProjectManager ProjectManager { get; }

    // 这个类只关心它自己需要的服务：IFileService 和 ProjectManager
    public FileActionsViewModel(IFileService fileService, ProjectManager projectManager)
    {
        _fileService = fileService;
        ProjectManager = projectManager;
    }

    [RelayCommand]
    private void ImportMusic()
    {
        string? filePath = _fileService.OpenAudioFile();
        if (filePath == null) return;

        try
        {
            ProjectManager.EditingProject.EncodedAudio = File.ReadAllBytes(filePath);

            // 发送消息通知 UI
            WeakReferenceMessenger.Default.Send(new AudioLoadedMessage(filePath));
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowFileActionError("import music", "Import Music Failed", filePath, $"Access to the audio file was denied.\n\n{ex.Message}");
        }
        catch (IOException ex)
        {
            ShowFileActionError("import music", "Import Music Failed", filePath, $"The audio file could not be read.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowFileActionError("import music", "Import Music Failed", filePath, $"An unexpected error occurred while importing music.\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void ImportIllustration()
    {
        string? filePath = _fileService.OpenImageFile();
        if (filePath == null) return;

        try
        {
            ProjectManager.EditingProject.EncodedIllustration = File.ReadAllBytes(filePath);
            WeakReferenceMessenger.Default.Send(new IllustrationLoadedMessage());
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowFileActionError("import illustration", "Import Illustration Failed", filePath, $"Access to the image file was denied.\n\n{ex.Message}");
        }
        catch (IOException ex)
        {
            ShowFileActionError("import illustration", "Import Illustration Failed", filePath, $"The image file could not be read.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowFileActionError("import illustration", "Import Illustration Failed", filePath, $"An unexpected error occurred while importing illustration.\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenProject()
    {
        string? filePath = _fileService.OpenProjectFile();
        if (filePath == null) return;

        try
        {
            ProjectManager.LoadEditingProject(filePath);
            WeakReferenceMessenger.Default.Send(new ProjectLoadedMessage());
        }
        catch (InvalidDataException ex)
        {
            ShowFileActionError("open project", "Open Project Failed", filePath, $"Project file is invalid.\n\n{ex.Message}");
        }
        catch (JsonException ex)
        {
            ShowFileActionError("open project", "Open Project Failed", filePath, $"Project data could not be parsed.\n\n{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowFileActionError("open project", "Open Project Failed", filePath, $"Access to the project file was denied.\n\n{ex.Message}");
        }
        catch (IOException ex)
        {
            ShowFileActionError("open project", "Open Project Failed", filePath, $"The project file could not be read.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowFileActionError("open project", "Open Project Failed", filePath, $"An unexpected error occurred while opening the project.\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveChart()
    {
        if (ProjectManager.EditingProject is null) return;

        if (ProjectManager.EditingProjectFilePath is null)
        {
            string? savePath = _fileService.SaveProjectFile("New Axphi Project");
            if (savePath == null) return;

            ProjectManager.EditingProjectFilePath = savePath;
        }

        try
        {
            ProjectManager.SaveEditingProject(ProjectManager.EditingProjectFilePath);
        }
        catch (ArgumentException ex)
        {
            ShowFileActionError("save project", "Save Project Failed", ProjectManager.EditingProjectFilePath, $"Project save path is invalid.\n\n{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowFileActionError("save project", "Save Project Failed", ProjectManager.EditingProjectFilePath, $"Access to the project file was denied.\n\n{ex.Message}");
        }
        catch (IOException ex)
        {
            ShowFileActionError("save project", "Save Project Failed", ProjectManager.EditingProjectFilePath, $"The project file could not be written.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowFileActionError("save project", "Save Project Failed", ProjectManager.EditingProjectFilePath, $"An unexpected error occurred while saving the project.\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportOfficialChart()
    {
        if (ProjectManager.EditingProject is null) return;

        string defaultFileName = "officialChart";
        if (!string.IsNullOrWhiteSpace(ProjectManager.EditingProjectFilePath))
        {
            defaultFileName = Path.GetFileNameWithoutExtension(ProjectManager.EditingProjectFilePath);
        }

        string? savePath = _fileService.SaveOfficialChartFile(defaultFileName);
        if (savePath == null) return;

        var setupDialog = new OfficialChartExportProgressDialog(setupMode: true);
        if (Application.Current?.MainWindow is Window mainWindow)
        {
            setupDialog.Owner = mainWindow;
        }

        bool? confirmed = setupDialog.ShowDialog();
        if (confirmed != true)
        {
            return;
        }

        var exportOptions = new OfficialChartExporter.ExportOptions(setupDialog.CalculateFloorPosition);

        var progressDialog = new OfficialChartExportProgressDialog();
        if (Application.Current?.MainWindow is Window progressOwner)
        {
            progressDialog.Owner = progressOwner;
        }

        progressDialog.UpdateProgress(0.0, "准备导出官谱...");

        var progress = new Progress<OfficialChartExporter.ExportProgress>(update =>
        {
            progressDialog.UpdateProgress(update.Fraction, update.Message);
        });

        try
        {
            progressDialog.Show();
            await Task.Run(() => OfficialChartExporter.ExportWithProgress(ProjectManager.EditingProject, savePath, progress, exportOptions));
        }
        catch (ArgumentException ex)
        {
            ShowFileActionError("export official chart", "Export Official Chart Failed", savePath, $"Export path is invalid.\n\n{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowFileActionError("export official chart", "Export Official Chart Failed", savePath, $"Access to the target file was denied.\n\n{ex.Message}");
        }
        catch (IOException ex)
        {
            ShowFileActionError("export official chart", "Export Official Chart Failed", savePath, $"The official chart file could not be written.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowFileActionError("export official chart", "Export Official Chart Failed", savePath, $"An unexpected error occurred while exporting official chart.\n\n{ex.Message}");
        }
        finally
        {
            if (progressDialog.IsVisible)
            {
                progressDialog.Close();
            }
        }
    }

    private static void ShowFileActionError(string actionName, string title, string? filePath, string details)
    {
        MessageBox.Show(
            $"Failed to {actionName}:\n{filePath ?? "(no path)"}\n\n{details}",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
