using System;
using System.Windows.Threading;

namespace Axphi.Utilities;

public sealed class TimelineContinuousDragCoordinator
{
    private readonly DispatcherTimer _timer;
    private readonly Func<double> _getPointerXOnSurface;
    private readonly Func<double> _getSurfaceWidth;
    private readonly Func<double> _getScrollValue;
    private readonly Action<double> _setScrollValue;
    private readonly Func<double> _getScrollMaximum;

    private Action? _activeUpdateAction;
    private bool _enableEdgeAutoScroll;

    public TimelineContinuousDragCoordinator(
        Func<double> getPointerXOnSurface,
        Func<double> getSurfaceWidth,
        Func<double> getScrollValue,
        Action<double> setScrollValue,
        Func<double> getScrollMaximum)
    {
        _getPointerXOnSurface = getPointerXOnSurface;
        _getSurfaceWidth = getSurfaceWidth;
        _getScrollValue = getScrollValue;
        _setScrollValue = setScrollValue;
        _getScrollMaximum = getScrollMaximum;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    public void Start(Action updateAction, bool enableEdgeAutoScroll)
    {
        _activeUpdateAction = updateAction;
        _enableEdgeAutoScroll = enableEdgeAutoScroll;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _activeUpdateAction = null;
        _enableEdgeAutoScroll = false;
    }

    public void Pulse()
    {
        _activeUpdateAction?.Invoke();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_activeUpdateAction == null)
        {
            return;
        }

        if (_enableEdgeAutoScroll)
        {
            const double edgeMargin = 30.0;
            const double speedMultiplier = 0.5;

            double pointerX = _getPointerXOnSurface();
            double surfaceWidth = _getSurfaceWidth();
            double scrollDelta = 0;

            if (pointerX > surfaceWidth - edgeMargin)
            {
                scrollDelta = (pointerX - (surfaceWidth - edgeMargin)) * speedMultiplier;
            }
            else if (pointerX < edgeMargin)
            {
                scrollDelta = (pointerX - edgeMargin) * speedMultiplier;
            }

            if (Math.Abs(scrollDelta) > double.Epsilon)
            {
                double nextScroll = _getScrollValue() + scrollDelta;
                nextScroll = Math.Clamp(nextScroll, 0, _getScrollMaximum());
                _setScrollValue(nextScroll);
            }
        }

        _activeUpdateAction.Invoke();
    }
}
