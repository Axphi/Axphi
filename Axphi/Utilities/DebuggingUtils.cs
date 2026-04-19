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


                BpmKeyFrames = new List<KeyFrame<double>>(),

                JudgementLines = new List<JudgementLine>()
                {
                    new JudgementLine()
                    {
                        Properties =
                        {
                            // Offset 已更新为 Position
                            Position =
                            {
                                KeyFrames =
                                {
                                    new KeyFrame<Vector>() { Time = SecToTick(0), Value = new Vector(0, 0), Easing = BezierPresets.Ease },
                                    new KeyFrame<Vector>() { Time = SecToTick(5), Value = new Vector(0, 1.5), Easing = BezierPresets.Ease },
                                    new KeyFrame<Vector>() { Time = SecToTick(8), Value = new Vector(0, -1.5), Easing = BezierPresets.Ease },
                                    new KeyFrame<Vector>() { Time = SecToTick(10), Value = new Vector(0, -1.5), Easing = BezierPresets.Ease },
                                    new KeyFrame<Vector>() { Time = SecToTick(12), Value = new Vector(-1.5, -1.5), Easing = BezierPresets.Ease },
                                }
                            },
                            Rotation =
                            {
                                KeyFrames =
                                {
                                    new KeyFrame<double>() { Time = SecToTick(0), Value = 0, Easing = BezierPresets.Ease },
                                    new KeyFrame<double>() { Time = SecToTick(8), Value = 0, Easing = BezierPresets.Ease },
                                    new KeyFrame<double>() { Time = SecToTick(10), Value = 90 },
                                }
                            },

                        },
                        Notes = new List<Note>()
                        {
                            //new Note(NoteKind.Tap, TimeSpan.FromSeconds(1)),
                            new Note(NoteKind.Tap, SecToTick(1)),
                            new Note(NoteKind.Tap, SecToTick(2))
                            {
                                Properties =
                                {
                                    // Note 的 Offset 也更新为 Position
                                    Position =
                                    {
                                        KeyFrames =
                                        {
                                            new KeyFrame<Vector>() { Time = SecToTick(0) },
                                            new KeyFrame<Vector>() { Time = SecToTick(1) },
                                            new KeyFrame<Vector>() { Time = SecToTick(1.5), Value = new Vector(-2, 0) },
                                            new KeyFrame<Vector>() { Time = SecToTick(1.75), Value = new Vector(2, 0) },
                                            new KeyFrame<Vector>() { Time = SecToTick(2) }
                                        }
                                    }
                                },
                            },
                            new Note(NoteKind.Tap, SecToTick(3))
                            {
                                Properties =
                                {
                                    Scale =
                                    {
                                        KeyFrames =
                                        {
                                            new KeyFrame<Vector>() { Time = SecToTick(0) },
                                            new KeyFrame<Vector>() { Time = SecToTick(2) },
                                            new KeyFrame<Vector>() { Time = SecToTick(2.5), Value = new Vector(2, 2) },
                                            new KeyFrame<Vector>() { Time = SecToTick(3), Value = new Vector(1, 1) }
                                        }
                                    }
                                },
                            },
                            new Note(NoteKind.Tap, SecToTick(4))
                            {
                                Properties =
                                {
                                    Rotation =
                                    {
                                        KeyFrames =
                                        {
                                            new KeyFrame<double>() { Time = SecToTick(0), Value = 0 },
                                            new KeyFrame<double>() { Time = SecToTick(3.5), Value = 0 },
                                            new KeyFrame<double>() { Time = SecToTick(4), Value = 180 }
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

                InitialBpm = 200,
                Duration = SecToTick(10),


                BpmKeyFrames = new List<KeyFrame<double>>()
                {

                },

                JudgementLines = new List<JudgementLine>()
                {
                    new JudgementLine()
                    {
                        Properties =
                        {
                            Position =
                            {
                                InitialValue = new Vector(0, -4)
                            },
                            Rotation =
                            {
                                KeyFrames = { }
                            },


                        },
                        Notes = new List<Note>()
                        {

                            new Note(NoteKind.Tap, SecToTick(1)),
                            new Note(NoteKind.Drag, SecToTick(2)),
                            new Note(NoteKind.Tap, SecToTick(3))
                            {
                                Properties =
                                {
                                    Rotation =
                                    {
                                        InitialValue = 45
                                    }
                                }

                            },
                            new Note(NoteKind.Tap, SecToTick(4)),
                            new Note(NoteKind.Tap, SecToTick(5)),
                            new Note(NoteKind.Tap, SecToTick(6)),
                            new Note(NoteKind.Tap, SecToTick(7)),

                        }
                    },
                    new JudgementLine(),
                    new JudgementLine()
                }
            };
        }
    }
}
