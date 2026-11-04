using System.Globalization;
using System.Security.Cryptography;
using SmartPassword.Core.Csv;

namespace SmartPassword.Core.Model;

public enum TimestampStyle { UnixSeconds, UnixMilliseconds, Iso8601, DateTimeMinutes, Unknown }
public enum IdStyle { OnePassword, Guid, Unknown }
public enum OtpStyle { Uri, Base32 }

/// <summary>
/// Everything needed to write entries back in the same format as the file they were imported from (spec 4.4).
/// </summary>
public sealed class FormatProfile
{
    public string SourceFileName { get; set; } = "";
    public DateTimeOffset? ImportedAt { get; set; }

    public bool HasBom { get; set; } = true;
    public string NewLine { get; set; } = "\r\n";
    public bool EndsWithNewLine { get; set; } = true;
    public char Separator { get; set; } = ',';

    /// <summary>Column names in file order.</summary>
    public List<string> Columns { get; set; } = [.. Model.Columns.Standard];

    /// <summary>Header row exactly as in the file; null for a vault that was never imported.</summary>
    public CsvRecord? HeaderRow { get; set; }

    public string FavoriteTrue { get; set; } = "true";
    public string FavoriteFalse { get; set; } = "false";
    public bool FavoriteUnknown { get; set; }
    public TimestampStyle TimestampStyle { get; set; } = TimestampStyle.UnixSeconds;
    public IdStyle IdStyle { get; set; } = IdStyle.OnePassword;
    public OtpStyle OtpStyle { get; set; } = OtpStyle.Uri;
    public string TagSeparator { get; set; } = ", ";
    public string DefaultCategory { get; set; } = "LOGIN";

    public bool IsFavorite(string value) =>
        value.Length > 0 && (value == FavoriteTrue ||
                             string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                             value == "1" || string.Equals(value, "ja", StringComparison.OrdinalIgnoreCase));

    public string FormatFavorite(bool favorite) => favorite ? FavoriteTrue : FavoriteFalse;

    public string FormatTimestamp(DateTimeOffset time) => TimestampStyle switch
    {
        TimestampStyle.UnixSeconds => time.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        TimestampStyle.UnixMilliseconds => time.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
        TimestampStyle.Iso8601 => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        TimestampStyle.DateTimeMinutes => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        _ => "",
    };

    public DateTimeOffset? ParseTimestamp(string value) => FieldFormats.ParseTimestamp(value, TimestampStyle);

    public string NewId() => IdStyle switch
    {
        IdStyle.OnePassword => RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz234567", 26),
        IdStyle.Guid => Guid.NewGuid().ToString("D"),
        _ => "",
    };
}
