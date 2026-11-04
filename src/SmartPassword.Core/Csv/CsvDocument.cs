namespace SmartPassword.Core.Csv;

/// <summary>
/// One CSV field. <see cref="Raw"/> is the exact text from the file (including any quotes),
/// <see cref="Value"/> is the unescaped content. Keeping both lets unchanged fields be written back byte-for-byte.
/// </summary>
public sealed class CsvField
{
    public string Raw { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class CsvRecord
{
    public List<CsvField> Fields { get; set; } = new();

    /// <summary>The line terminator that followed this record in the file ("\r\n", "\n", "\r" or "" at end of file).</summary>
    public string LineEnding { get; set; } = "";

    /// <summary>1-based physical line on which the record starts.</summary>
    public int LineNumber { get; set; }
}

public sealed class CsvDocument
{
    public bool HasBom { get; set; }
    public List<CsvRecord> Records { get; set; } = new();
}

public sealed class CsvFormatException(string message, int lineNumber) : Exception(message)
{
    public int LineNumber { get; } = lineNumber;
}
