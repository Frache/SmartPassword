using System.Text;

namespace SmartPassword.Core.Csv;

/// <summary>
/// RFC 4180 parser that records the raw text of every field and every line terminator,
/// so a document can be written back without any byte changes.
/// </summary>
public static class CsvParser
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static CsvDocument Parse(byte[] bytes, char separator = ',')
    {
        var hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
        var body = hasBom ? bytes.AsSpan(Utf8Bom.Length) : bytes.AsSpan();

        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(body);
        }
        catch (DecoderFallbackException)
        {
            throw new CsvFormatException("Filen er ikke gyldig UTF-8.", 1);
        }

        var document = Parse(text, separator);
        document.HasBom = hasBom;
        return document;
    }

    public static CsvDocument Parse(string text, char separator = ',')
    {
        var document = new CsvDocument();
        var pos = 0;
        var line = 1;

        while (pos < text.Length)
        {
            var record = new CsvRecord { LineNumber = line };

            while (true)
            {
                var start = pos;
                string value;

                if (pos < text.Length && text[pos] == '"')
                {
                    var fieldLine = line;
                    var sb = new StringBuilder();
                    pos++;
                    while (true)
                    {
                        if (pos >= text.Length)
                            throw new CsvFormatException($"Uafsluttet anførselstegn i felt, der starter på linje {fieldLine}.", fieldLine);

                        var c = text[pos];
                        if (c == '"')
                        {
                            if (pos + 1 < text.Length && text[pos + 1] == '"')
                            {
                                sb.Append('"');
                                pos += 2;
                                continue;
                            }
                            pos++;
                            break;
                        }
                        if (c == '\n' || (c == '\r' && (pos + 1 >= text.Length || text[pos + 1] != '\n')))
                            line++;
                        sb.Append(c);
                        pos++;
                    }

                    if (pos < text.Length && text[pos] != separator && text[pos] != '\r' && text[pos] != '\n')
                        throw new CsvFormatException($"Uventet tegn efter afsluttende anførselstegn på linje {line}.", line);

                    value = sb.ToString();
                }
                else
                {
                    while (pos < text.Length && text[pos] != separator && text[pos] != '\r' && text[pos] != '\n')
                        pos++;
                    value = text[start..pos];
                }

                record.Fields.Add(new CsvField { Raw = text[start..pos], Value = value });

                if (pos < text.Length && text[pos] == separator)
                {
                    pos++;
                    continue;
                }
                break;
            }

            if (pos < text.Length)
            {
                if (text[pos] == '\r' && pos + 1 < text.Length && text[pos + 1] == '\n')
                {
                    record.LineEnding = "\r\n";
                    pos += 2;
                }
                else
                {
                    record.LineEnding = text[pos].ToString();
                    pos++;
                }
                line++;
            }

            document.Records.Add(record);
        }

        return document;
    }
}
