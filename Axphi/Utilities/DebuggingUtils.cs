using Axphi.Data;
using Axphi.Data.KeyFrames;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Axphi.Utilities
{
    internal static class DebuggingUtils
    {

        // 【新增辅助方法】：将人类可读的“秒”转换为谱面底层的“Tick (128分音符)”
        // 根据公式：1个Tick的时间 = 1.875 / BPM
        private static int SecToTick(double seconds, double bpm = 120.0)
        {
            // Tick数 = 秒数 / 每个Tick的秒数
            return (int)(seconds * bpm / 1.875);
        }

        public static Chart CreateDemoChart()
        {
            return new Chart()
            {
                formatVersion = "1.0",

                //Duration = TimeSpan.FromSeconds(60),
                Duration = SecToTick(60),

                // 【修改 2】：在这里填写 BPM！这里支持变速，所以是关键帧数组。测试谱面我们先写一个固定的 120 BPM。
                BpmKeyFrames = new List<KeyFrame<double>>()
                {
                    
                },

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

                                        Time = SecToTick(0),
                                        Value = new Vector(0, 0),
                                        Easing = BezierPresets.Ease
                                    },

                                    new Data.KeyFrames.OffsetKeyFrame()
                                    {
                                        //Time = TimeSpan.FromSeconds(5),
                                        Time = SecToTick(5),
                                        Value = new Vector(0, 1.5),

                                        Easing = BezierPresets.Ease
                                        
                                    },

                                    new Data.KeyFrames.OffsetKeyFrame()
                                    {
                                        Time = SecToTick(8),
                                        Value = new Vector(0, -1.5),
                                        Easing = BezierPresets.Ease
                                    },
                                    new Data.KeyFrames.OffsetKeyFrame()
                                    {
                                        Time = SecToTick(10),
                                        Value = new Vector(0, -1.5),
                                        Easing = BezierPresets.Ease
                                    },
                                    new Data.KeyFrames.OffsetKeyFrame()
                                    {
                                        Time = SecToTick(12),
                                        Value = new Vector(-1.5, -1.5),
                                        Easing = BezierPresets.Ease
                                    },
                                }
                            },
                            Rotation=
                            {
                                KeyFrames=
                                {
                                    new Data.KeyFrames.RotationKeyFrame()
                                    {
                                        Time = SecToTick(0),
                                        Value = 0,
                                        Easing = BezierPresets.Ease
                                    },
                                    new Data.KeyFrames.RotationKeyFrame()
                                    {
                                        Time = SecToTick(8),
                                        Value = 0,
                                        Easing = BezierPresets.Ease
                                    },
                                    new Data.KeyFrames.RotationKeyFrame()
                                    {
                                        Time = SecToTick(10),
                                        Value = 90,
                                    }
                                }
                            },

                        },
                        Notes = new List<Note>()
                        {
                            //new Note(NoteKind.Tap, TimeSpan.FromSeconds(1)),
                            new Note(NoteKind.Tap, SecToTick(1)),
                            new Note(NoteKind.Tap, SecToTick(2))
                            {
                                AnimatableProperties =
                                {
                                    Offset =
                                    {
                                        KeyFrames =
                                        {
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = SecToTick(0),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = SecToTick(1),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = SecToTick(1.5),
                                                Value = new Vector(-2, 0),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = SecToTick(1.75),
                                                Value = new Vector(2, 0),
                                            },
                                            new Data.KeyFrames.OffsetKeyFrame()
                                            {
                                                Time = SecToTick(2),
                                            }
                                        }
                                    }
                                },
                            },
                            new Note(NoteKind.Tap, SecToTick(3))
                            {
                                AnimatableProperties =
                                {
                                    Scale =
                                    {
                                        KeyFrames =
                                        {
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = SecToTick(0),
                                            },
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = SecToTick(2),
                                            },
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = SecToTick(2.5),
                                                Value = new Vector(2, 2)
                                            },
                                            new Data.KeyFrames.ScaleKeyFrame()
                                            {
                                                Time = SecToTick(3),
                                                Value = new Vector(1, 1)
                                            }
                                        }
                                    }
                                },
                            },
                            new Note(NoteKind.Tap, SecToTick(4))
                            {
                                AnimatableProperties =
                                {
                                    Rotation =
                                    {
                                        KeyFrames =
                                        {
                                            new Data.KeyFrames.RotationKeyFrame()
                                            {
                                                Time = SecToTick(0),
                                                Value = 0
                                            },
                                            new Data.KeyFrames.RotationKeyFrame()
                                            {
                                                Time = SecToTick(3.5),
                                                Value = 0
                                            },
                                            new Data.KeyFrames.RotationKeyFrame()
                                            {
                                                Time = SecToTick(4),
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
        public static Chart CreateDemoChart2()
        {
            return new Chart()
            {
                formatVersion = "1.0",


                Duration = SecToTick(60),


                BpmKeyFrames = new List<KeyFrame<double>>()
                {

                },

                JudgementLines = new List<JudgementLine>()
                {
                    new JudgementLine()
                    {
                        AnimatableProperties =
                        {
                            Offset=
                            {
                                InitialValue = new Vector(0, -4)
                            },
                            Rotation=
                            {
                                KeyFrames=
                                {

                                }
                            },

                        },
                        Notes = new List<Note>()
                        {

                            new Note(NoteKind.Tap, SecToTick(1)),
                            new Note(NoteKind.Tap, SecToTick(2)),
                            new Note(NoteKind.Tap, SecToTick(3)),
                            new Note(NoteKind.Tap, SecToTick(4)),
                            new Note(NoteKind.Tap, SecToTick(5)),
                            new Note(NoteKind.Tap, SecToTick(6)),
                            new Note(NoteKind.Tap, SecToTick(7)),

                        }
                    }
                }
            };
        }
    }
}
