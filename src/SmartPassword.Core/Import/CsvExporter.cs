using SmartPassword.Core.Csv;
using SmartPassword.Core.Model;

namespace SmartPassword.Core.Import;

/// <summary>
/// Writes entries back in the format described by a <see cref="FormatProfile"/> (spec 4.3).
/// Fields whose value is unchanged since import are written with their original raw text,
/// so an untouched vault exports byte-identical to the imported file.
/// </summary>
public static class CsvExporter
{
    public static byte[] Export(FormatProfile profile, IReadOnlyList<PasswordEntry> entries)
    {
        var header = new CsvRecord
        {
            Fields = profile.HeaderRow?.Fields.ToList()
                     ?? profile.Columns.Select(c => new CsvField { Raw = CsvWriter.Quote(c), Value = c }).ToList(),
            LineEnding = profile.HeaderRow?.LineEnding ?? "",
        };

        var records = new List<CsvRecord> { header };
        foreach (var entry in entries)
        {
            var record = new CsvRecord { LineEnding = entry.Source?.LineEnding ?? "" };
            for (var i = 0; i < profile.Columns.Count; i++)
            {
                var value = Columns.Get(entry, profile.Columns[i]);
                var source = entry.Source != null && i < entry.Source.Fields.Count ? entry.Source.Fields[i] : null;
                record.Fields.Add(source != null && source.Value == value
                    ? source
                    : new CsvField { Raw = CsvWriter.Quote(value), Value = value });
            }
            records.Add(record);
        }

        // Rows keep their own terminator; rows that had none (new rows, or the old last row) get the file's newline,
        // and the final row follows the original "ends with newline" setting.
        for (var i = 0; i < records.Count; i++)
        {
            var isLast = i == records.Count - 1;
            if (isLast && !profile.EndsWithNewLine)
                records[i].LineEnding = "";
            else if (records[i].LineEnding.Length == 0)
                records[i].LineEnding = profile.NewLine;
        }

        return CsvWriter.Write(new CsvDocument { HasBom = profile.HasBom, Records = records }, profile.Separator);
    }
}
