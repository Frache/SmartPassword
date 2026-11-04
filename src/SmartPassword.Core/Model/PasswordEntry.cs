using SmartPassword.Core.Csv;

namespace SmartPassword.Core.Model;

/// <summary>
/// One login. All values are kept as the exact strings used in the source file, so nothing is
/// reformatted unless the user changes it. Typed access (favorite, timestamps) goes through <see cref="FormatProfile"/>.
/// </summary>
public sealed class PasswordEntry
{
    /// <summary>Internal key, never exported.</summary>
    public Guid InternalId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Website { get; set; } = "";
    public string Otp { get; set; } = "";
    public string Notes { get; set; } = "";
    public string OtherFields { get; set; } = "";
    public string Tags { get; set; } = "";
    public string Favorite { get; set; } = "";
    public string Created { get; set; } = "";
    public string Updated { get; set; } = "";
    public string Id { get; set; } = "";

    /// <summary>Values of columns the program does not know, keyed by column name.</summary>
    public Dictionary<string, string> ExtraColumns { get; set; } = new();

    /// <summary>The row as it was in the imported file; used to write unchanged fields back byte-for-byte.</summary>
    public CsvRecord? Source { get; set; }

    public PasswordEntry Clone()
    {
        var copy = (PasswordEntry)MemberwiseClone();
        copy.ExtraColumns = new Dictionary<string, string>(ExtraColumns);
        return copy;
    }

    /// <summary>True if any user-visible value differs from <paramref name="other"/>.</summary>
    public bool ContentEquals(PasswordEntry other) =>
        Name == other.Name && Category == other.Category && Username == other.Username &&
        Password == other.Password && Website == other.Website && Otp == other.Otp &&
        Notes == other.Notes && OtherFields == other.OtherFields && Tags == other.Tags &&
        Favorite == other.Favorite && Created == other.Created && Updated == other.Updated && Id == other.Id &&
        ExtraColumns.Count == other.ExtraColumns.Count &&
        ExtraColumns.All(kv => other.ExtraColumns.TryGetValue(kv.Key, out var v) && v == kv.Value);

    public IEnumerable<string> TagList(string separator = ", ") =>
        Tags.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
