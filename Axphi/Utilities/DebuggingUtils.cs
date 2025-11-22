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
                        AnimatableProperties =
                        {
                            Offset=
                            {
                                KeyFrames =
                                {
                                    new Data.KeyFrames.OffsetKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(5),
                                        Value = new Vector(0, 1.5),
                                    },

                                    new Data.KeyFrames.OffsetKeyFrame()
                                    {
                                        Time = TimeSpan.FromSeconds(8),
                                        Value = new Vector(0, -1.5),
                                    }
                                }
                            }
                        },
                        Notes = new List<Note>()
                        {
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(1)),
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(2))
                            {
                                AnimatableProperties =
                                {
                                    Offset =
                                    {
                                        KeyFrames =
                                        {
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(1),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(1.5),
                                                Value = new Vector(-2, 0),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(1.75),
                                                Value = new Vector(2, 0),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(2),
                                            }
                                        }
                                    }
                                },
                            },
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(3))
                            {
                                AnimatableProperties =
                                {
                                    Scale =
                                    {
                                        KeyFrames =
                                        {
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(2),
                                            },
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(2.5),
                                                Value = new Vector(2, 2)
                                            },
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(3),
                                                Value = new Vector(1, 1)
                                            }
                                        }
                                    }
                                },
                            },
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(4))
                            {
                                AnimatableProperties =
                                {
                                    Rotation =
                                    {
                                        KeyFrames =
                                        {
                                            new Data.KeyFrames.RotationKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(3.5),
                                                Value = 0
                                            },
                                            new Data.KeyFrames.RotationKeyFrame()
                                            {
                                                Time = TimeSpan.FromSeconds(4),
                                                Value = 180
                                            }
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
