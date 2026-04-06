using System;
using System.Collections.Generic;
using System.Text;

namespace Axphi.Abstraction
{
    /// <summary>
    /// 播放时间同步提供器
    /// </summary>
    public interface IPlayTimeSyncProvider
    {
        public void Start();
        public void Pause();
        public void Stop();


        public bool IsRunning { get; }
        public TimeSpan Time { get; set; }

        public event EventHandler? Updated;
    }
}
