using Axphi.Data;
using Axphi.Services;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Axphi.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        // 贝塞尔曲线 ViewModel
        public BezierViewModel BezierViewModel { get; }

        // 文件操作 ViewModel
        public FileActionsViewModel FileActions { get; }

        // 轨道头 ViewModel
        public TrackHeaderViewModel TrackHeaderViewModel { get; }

        // 时间线 ViewModel
        public TimeLineViewModel TimeLineViewModel { get; }

        // 谱面显示 ViewModel
        public ChartDisplayViewModel ChartDisplayViewModel { get; }

        // 项目管理器
        public ProjectManager ProjectManager { get; }


        


        // 通过依赖注入，把它们接进来
        public MainViewModel(
            BezierViewModel bezierVM,
            ProjectManager projectManager,
            FileActionsViewModel fileActions,
            TrackHeaderViewModel trackHeaderViewModel,
            TimeLineViewModel timeLineViewModel,
            ChartDisplayViewModel chartDisplayViewModel
            )
        {
            BezierViewModel = bezierVM;
            ProjectManager = projectManager;
            FileActions = fileActions;
            TrackHeaderViewModel = trackHeaderViewModel;
            TimeLineViewModel = timeLineViewModel;
            ChartDisplayViewModel = chartDisplayViewModel;
            
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

        public void Dispose()
        {
            ChartDisplayViewModel.Dispose();
            TimeLineViewModel.Dispose();
            TrackHeaderViewModel.Dispose();
        }

    }
}
