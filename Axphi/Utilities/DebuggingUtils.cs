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
                JudgementLines =
                {
                    new JudgementLine()
                    {
                        Offset=
                        {
                            KeyFrames =
                            {
                                new()
                                {
                                    Time = TimeSpan.FromSeconds(5),
                                    Value = new Vector(0, 1.5),
                                },

                                new()
                                {
                                    Time = TimeSpan.FromSeconds(8),
                                    Value = new Vector(0, -1.5),
                                }
                            }
                        },
                        Notes =
                        {
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(1)),
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(2))
                            {
                                Offset =
                                {
                                    KeyFrames =
                                    {
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(1),
                                        },
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(1.5),
                                            Value = new Vector(-2, 0),
                                        },
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(1.75),
                                            Value = new Vector(2, 0),
                                        },
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(2),
                                        }
                                    }
                                }
                            },
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(3))
                            {
                                Scale =
                                {
                                    KeyFrames =
                                    {
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(2),
                                        },
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(2.5),
                                            Value = new Vector(2, 2)
                                        },
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(3),
                                            Value = new Vector(1, 1)
                                        }
                                    }
                                }
                            },
                            new Note(NoteKind.Tap, TimeSpan.FromSeconds(4))
                            {
                                Rotation =
                                {
                                    KeyFrames =
                                    {
                                        new()
                                        {
                                            Time = TimeSpan.FromSeconds(3.5),
                                            Value = 0
                                        },
                                        new()
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
            };
        }
    }
}
