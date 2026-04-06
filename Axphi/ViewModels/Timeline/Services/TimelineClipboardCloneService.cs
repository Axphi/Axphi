using Axphi.Data;
using Axphi.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axphi.ViewModels;

public sealed class TimelineClipboardCloneService : ITimelineClipboardCloneService
{
    private static readonly JsonSerializerOptions CloneJsonSerializerOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new VectorJsonConverter() }
    };

    public Note CloneNote(Note note)
    {
        var clonedNote = JsonSerializer.Deserialize<Note>(
                JsonSerializer.Serialize(note, CloneJsonSerializerOptions),
                CloneJsonSerializerOptions)
            ?? new Note();

        clonedNote.ID = Guid.NewGuid().ToString();
        return clonedNote;
    }

    public JudgementLine CloneJudgementLine(JudgementLine line)
    {
        var clonedLine = JsonSerializer.Deserialize<JudgementLine>(
                JsonSerializer.Serialize(line, CloneJsonSerializerOptions),
                CloneJsonSerializerOptions)
            ?? new JudgementLine();

        clonedLine.ID = Guid.NewGuid().ToString();
        clonedLine.Notes ??= new List<Note>();
        foreach (var note in clonedLine.Notes)
        {
            note.ID = Guid.NewGuid().ToString();
        }

        return clonedLine;
    }
}
