using Axphi.Data;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Text;
using System.Transactions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Axphi;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DispatcherTimer? _dispatcherTimer;
    private Stopwatch? _renderStopwatch;

    public MainWindow()
    {
        DataContext = this;
        InitializeComponent();

        chartRenderer.Chart = new Chart()
        {
            Duration = TimeSpan.FromSeconds(60),
            JudgementLines = new List<JudgementLine>()
            {
                new JudgementLine()
                {
                    TransformKeyFrames = new Data.KeyFrames.TransformKeyFrames()
                    {
                        OffsetKeyFrames = new List<Data.KeyFrames.VectorKeyFrame>()
                        {
                            new Data.KeyFrames.VectorKeyFrame()
                            {
                                Time = TimeSpan.FromSeconds(5),
                                Vector = new Vector(0, 1.5),
                            },

                            new Data.KeyFrames.VectorKeyFrame()
                            {
                                Time = TimeSpan.FromSeconds(8),
                                Vector = new Vector(0, -1.5),
                            }
                        }
                    },
                    Nodes = new List<Node>()
                    {
                        new Node(NodeKind.Tap, TimeSpan.FromSeconds(1)),
                        new Node(NodeKind.Tap, TimeSpan.FromSeconds(2))
                        {
                            TransformKeyFrames = new Data.KeyFrames.TransformKeyFrames()
                            {
                                OffsetKeyFrames = new List<Data.KeyFrames.VectorKeyFrame>()
                                {
                                    new Data.KeyFrames.VectorKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(1),
                                    },
                                    new Data.KeyFrames.VectorKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(1.5),
                                        Vector = new Vector(-2, 0),
                                    },
                                    new Data.KeyFrames.VectorKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(1.75),
                                        Vector = new Vector(2, 0),
                                    },
                                    new Data.KeyFrames.VectorKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(2),
                                    }
                                }
                            }
                        },
                        new Node(NodeKind.Tap, TimeSpan.FromSeconds(3))
                        {
                            TransformKeyFrames = new Data.KeyFrames.TransformKeyFrames()
                            {
                                ScaleKeyFrames = new List<Data.KeyFrames.ScaleKeyFrame>()
                                {
                                    new Data.KeyFrames.ScaleKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(2),
                                    },
                                    new Data.KeyFrames.ScaleKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(2.5),
                                        Scale = new Vector(2, 2)
                                    },
                                    new Data.KeyFrames.ScaleKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(3),
                                        Scale = new Vector(1, 1)
                                    }
                                }
                            }
                        },
                        new Node(NodeKind.Tap, TimeSpan.FromSeconds(4))
                        {
                            TransformKeyFrames = new Data.KeyFrames.TransformKeyFrames()
                            {
                                RotationKeyFrames = new List<Data.KeyFrames.RotationKeyFrame>()
                                {
                                    new Data.KeyFrames.RotationKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(3.5),
                                        Angle = 0
                                    },
                                    new Data.KeyFrames.RotationKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(4),
                                        Angle = 180
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    [RelayCommand]
    private void PlayPauseChartRendering()
    {
        _renderStopwatch ??= new Stopwatch();
        if (_dispatcherTimer is null)
        {
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Render, RenderTimerCallback, Dispatcher);
        }
        else
        {
            _dispatcherTimer.IsEnabled ^= true;
        }

        if (_dispatcherTimer.IsEnabled)
        {
            _renderStopwatch.Start();
        }
        else
        {
            _renderStopwatch.Stop();
        }
    }

    [RelayCommand]
    private void StopChartRendering()
    {
        _renderStopwatch?.Stop();
        _renderStopwatch?.Reset();
        _dispatcherTimer?.Stop();

        chartRenderer.Time = default;
    }

    private void RenderTimerCallback(object? sender, EventArgs e)
    {
        _renderStopwatch ??= new Stopwatch();
        chartRenderer.Time = _renderStopwatch.Elapsed;
    }
}