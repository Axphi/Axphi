using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Axphi.Services;

public partial class PlaybackPositionService : ObservableObject
{
    [ObservableProperty]
    private TimeSpan _currentTime;

    [ObservableProperty]
    private int _currentTick;

    public void Reset()
    {
        CurrentTime = TimeSpan.Zero;
        CurrentTick = 0;
    }
}
