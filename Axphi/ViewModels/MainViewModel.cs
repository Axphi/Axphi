using Axphi.Services; // 确保引入了 ProjectManager 所在的命名空间
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging; // Messenger 需要用到这个
using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 贝塞尔曲线 ViewModel
        public BezierViewModel BezierViewModel { get; }

        // 项目管理器
        public ProjectManager ProjectManager { get; }

        // 文件处理
        private readonly IFileService FileService;
        public FileActionsViewModel FileActions { get; }

        // 通过依赖注入，把它们接进来
        public MainViewModel(
            BezierViewModel bezierVM,
            ProjectManager projectManager,
            IFileService fileService,
            FileActionsViewModel fileActionsViewModel)
        {
            BezierViewModel = bezierVM;
            ProjectManager = projectManager;
            FileService = fileService;
            FileActions = fileActionsViewModel;
        }

        
    }
}
