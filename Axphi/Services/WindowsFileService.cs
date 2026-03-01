using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace Axphi.Services
{
    internal class WindowsFileService : IFileService
    {
        public string? OpenAudioFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import music",
                Filter = "Audio file|*.mp3;*.ogg;*.wav|Any|*.*",
                CheckFileExists = true
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? SaveProjectFile(string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Chart",
                FileName = defaultFileName,
                Filter = "Axphi Project|*.axp|Any File|*.*",
                CheckPathExists = true
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
