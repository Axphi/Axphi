using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Axphi.Utilities
{
    internal static class DebuggingUtils
    {
        public static Chart CreateDemoChart()
        {
            return new Chart()
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
                        Notes = new List<Note>()
                        {
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(1)),
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(2))
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
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(3))
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
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(4))
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
    }
}
