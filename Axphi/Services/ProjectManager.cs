using Axphi.Data;
using Axphi.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Axphi.Services
{
    public partial class ProjectManager : ObservableObject
    {
        [ObservableProperty]
        private Project? _editingProject;

        [ObservableProperty]
        private string? _editingProjectFilePath;

        public void SaveProject(Project project, string path)
        {
            var package = Package.Open(path, System.IO.FileMode.Create);
            var chartJson = JsonSerializer.Serialize(project.Chart);
            var chartJsonUtfBytes = Encoding.UTF8.GetBytes(chartJson);

            package.WritePartAllBytes("chart.json", "application/json", chartJsonUtfBytes);
            package.WritePartAllBytes("audio", "audio", project.EncodedAudio);
            package.WritePartAllBytes("illustration", "audio", project.EncodedIllustration);
        }

        public void SaveEditingProject(string path)
        {
            if (EditingProject is null)
            {
                throw new InvalidOperationException("No project editing");
            }

            SaveProject(EditingProject, path);
        }
    }
}
