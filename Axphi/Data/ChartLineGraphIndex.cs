using System;
using System.Collections.Generic;

namespace Axphi.Data
{
    public sealed class ChartLineGraphIndex
    {
        private readonly Dictionary<string, JudgementLine> _lineById;

        private ChartLineGraphIndex(Dictionary<string, JudgementLine> lineById)
        {
            _lineById = lineById;
        }

        public IReadOnlyDictionary<string, JudgementLine> LineById => _lineById;

        public static ChartLineGraphIndex Build(Chart chart)
        {
            var lineById = new Dictionary<string, JudgementLine>(StringComparer.Ordinal);
            foreach (var line in chart.JudgementLines)
            {
                if (!string.IsNullOrWhiteSpace(line.ID) && !lineById.ContainsKey(line.ID))
                {
                    lineById[line.ID] = line;
                }
            }

            return new ChartLineGraphIndex(lineById);
        }

        public void ApplyHierarchy(Chart chart)
        {
            foreach (var line in chart.JudgementLines)
            {
                line.ParentChart = chart;
                line.ParentLine = null;

                foreach (var note in line.Notes)
                {
                    note.ParentLine = line;
                }
            }

            foreach (var line in chart.JudgementLines)
            {
                if (!string.IsNullOrWhiteSpace(line.ParentLineId)
                    && _lineById.TryGetValue(line.ParentLineId, out var parentLine))
                {
                    line.ParentLine = parentLine;
                }
            }
        }
    }
}
