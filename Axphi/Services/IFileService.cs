using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.Services
{
    public interface IFileService
    {
        // 返回选中的文件路径，取消则返回 null
        string? OpenAudioFile();
        string? OpenImageFile();
        string? OpenProjectFile();
        string? SaveProjectFile(string defaultFileName);
    }
}
