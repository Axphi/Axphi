using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;





using CommunityToolkit.Mvvm.Messaging;
using Axphi.Services;
using System.IO;

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

        ProjectManager.EditingProject.EncodedAudio = File.ReadAllBytes(filePath);

        // 发送消息通知 UI
        WeakReferenceMessenger.Default.Send(new AudioLoadedMessage(filePath));
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

        ProjectManager.SaveEditingProject(ProjectManager.EditingProjectFilePath);
    }
}
