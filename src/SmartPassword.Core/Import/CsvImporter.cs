using SmartPassword.Core.Csv;
using SmartPassword.Core.Model;

namespace SmartPassword.Core.Import;

public sealed record ImportResult(FormatProfile Profile, List<PasswordEntry> Entries, List<string> Warnings);

/// <summary>Reads a 1Password CSV export into entries plus a <see cref="FormatProfile"/> (spec 4.2).</summary>
public static class CsvImporter
{
    public static ImportResult Import(byte[] bytes, string sourceFileName = "")
    {
        var document = CsvParser.Parse(bytes);
        if (document.Records.Count == 0)
            throw new CsvFormatException("Filen er tom.", 1);

        var header = document.Records[0];
        var columns = header.Fields.Select(f => f.Value).ToList();

        var missing = Columns.Standard.Where(c => !columns.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new CsvFormatException($"Header mangler kolonnerne: {string.Join(", ", missing)}.", header.LineNumber);

        var duplicate = columns.GroupBy(c => c).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new CsvFormatException($"Kolonnen '{duplicate.Key}' findes flere gange i header.", header.LineNumber);

        var entries = new List<PasswordEntry>();
        foreach (var record in document.Records.Skip(1))
        {
            if (record.Fields.Count != columns.Count)
                throw new CsvFormatException(
                    $"Linje {record.LineNumber} har {record.Fields.Count} felter, men header har {columns.Count}.", record.LineNumber);

            var entry = new PasswordEntry { Source = record };
            for (var i = 0; i < columns.Count; i++)
                Columns.Set(entry, columns[i], record.Fields[i].Value);
            entries.Add(entry);
        }

        var last = document.Records[^1];
        var profile = new FormatProfile
        {
            SourceFileName = sourceFileName,
            ImportedAt = DateTimeOffset.Now,
            HasBom = document.HasBom,
            NewLine = document.Records.Select(r => r.LineEnding).FirstOrDefault(e => e.Length > 0) ?? "\r\n",
            EndsWithNewLine = last.LineEnding.Length > 0,
            Columns = columns,
            HeaderRow = header,
        };

        var categories = entries.Select(e => e.Category).Where(c => c.Length > 0).ToList();
        if (categories.Count > 0 && !categories.Contains(profile.DefaultCategory))
            profile.DefaultCategory = categories.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;

        var warnings = FieldFormats.Detect(profile, entries);
        return new ImportResult(profile, entries, warnings);
    }
}
