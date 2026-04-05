using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Axphi.Data;
using Axphi.Data.KeyFrames;

var asm = Assembly.LoadFrom('Axphi/bin/Debug/net10.0-windows/Axphi.dll');
var exporter = asm.GetType('Axphi.Utilities.OfficialChartExporter', true)!;
var exportMethod = exporter.GetMethod('Export', BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

void Dump(Project project, string label)
{
    string path = Path.Combine(Path.GetTempPath(), $"{label}-{Guid.NewGuid():N}.json");
    exportMethod.Invoke(null, new object[] { project, path });
    Console.WriteLine($"=== {label} ===");
    Console.WriteLine(File.ReadAllText(path));
    File.Delete(path);
}

var note1 = new Note(NoteKind.Tap, 4);
note1.AnimatableProperties.Offset.KeyFrames.AddRange(new[]
{
    new OffsetKeyFrame { Time = 0, Value = new Vector(0, 0) },
    new OffsetKeyFrame { Time = 4, Value = new Vector(2, 0) }
});
Dump(new Project
{
    Chart = new Chart { Duration = 4, JudgementLines = { new JudgementLine { Notes = { note1 } } } },
    Metadata = new ProjectMetadata { TotalDurationTicks = 4 }
}, 'xcarrier');

var note2 = new Note(NoteKind.Tap, 4);
note2.AnimatableProperties.Rotation.KeyFrames.AddRange(new[]
{
    new RotationKeyFrame { Time = 0, Value = 0 },
    new RotationKeyFrame { Time = 4, Value = 40 }
});
Dump(new Project
{
    Chart = new Chart { Duration = 4, JudgementLines = { new JudgementLine { Notes = { note2 } } } },
    Metadata = new ProjectMetadata { TotalDurationTicks = 4 }
}, 'rotcarrier');
