using System.Text;

namespace SmartPassword.Core.Csv;

public static class CsvWriter
{
    public static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    public static byte[] Write(CsvDocument document, char separator = ',')
    {
        var sb = new StringBuilder();
        foreach (var record in document.Records)
        {
            for (var i = 0; i < record.Fields.Count; i++)
            {
                if (i > 0) sb.Append(separator);
                sb.Append(record.Fields[i].Raw);
            }
            sb.Append(record.LineEnding);
        }

        var body = new UTF8Encoding(false).GetBytes(sb.ToString());
        if (!document.HasBom) return body;

        var result = new byte[body.Length + 3];
        result[0] = 0xEF; result[1] = 0xBB; result[2] = 0xBF;
        body.CopyTo(result, 3);
        return result;
    }
}
