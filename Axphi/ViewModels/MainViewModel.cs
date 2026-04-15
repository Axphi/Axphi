using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
        

        // 通过依赖注入，把它们接进来
        public MainViewModel(
            BezierViewModel bezierVM,
            ProjectManager projectManager,
            FileActionsViewModel fileActions)
        {
            BezierViewModel = bezierVM;
            ProjectManager = projectManager;
            FileActions = fileActions;
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

        [RelayCommand]
        private void LoadDemoChart2()
        {
            ProjectManager.EditingProject = new Project()
            {
                Chart = DebuggingUtils.CreateDemoChart2()
            };
            ProjectManager.EditingProjectFilePath = null;
        }

    }
}
