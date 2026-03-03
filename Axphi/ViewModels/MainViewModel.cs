using Axphi.Data;
using Axphi.Services; // 确保引入了 ProjectManager 所在的命名空间
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        
        public FileActionsViewModel FileActions { get; }

        // 注册时间轴子 ViewModel
        public TimelineViewModel Timeline { get; } = new TimelineViewModel();

        

        // 通过依赖注入，把它们接进来
        public MainViewModel(
            BezierViewModel bezierVM,
            ProjectManager projectManager,
            
            FileActionsViewModel fileActionsViewModel,
            TimelineViewModel timelineViewModel


            )
        {
            BezierViewModel = bezierVM;
            ProjectManager = projectManager;
            
            FileActions = fileActionsViewModel;
            Timeline = timelineViewModel;
            

        }
        [RelayCommand]
        private void LoadDemoChart()
        {
            ProjectManager.EditingProject = new Project()
            {
                Chart = DebuggingUtils.CreateDemoChart()
            };
            ProjectManager.EditingProjectFilePath = null;
        }


    }
}
