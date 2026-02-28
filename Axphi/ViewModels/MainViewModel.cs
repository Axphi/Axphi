using System;
using System.Collections.Generic;
using System.Text;

using Axphi.Services; // 确保引入了 ProjectManager 所在的命名空间
using CommunityToolkit.Mvvm.ComponentModel;

namespace Axphi.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // 贝塞尔曲线 ViewModel
        public BezierViewModel BezierViewModel { get; }

        // 项目管理器
        public ProjectManager ProjectManager { get; }

        // 通过依赖注入，把它们接进来
        public MainViewModel(
            BezierViewModel bezierVM,
            ProjectManager projectManager)
        {
            BezierViewModel = bezierVM;
            ProjectManager = projectManager;
        }
    }
}
