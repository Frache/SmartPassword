using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartPassword.Core.Model;

/// <summary>Detects the per-column value formats of an imported file (spec F-78, assumptions A-1..A-4).</summary>
public static partial class FieldFormats
{
    private static readonly (string True, string False)[] FavoritePairs =
    [
        ("true", "false"), ("TRUE", "FALSE"), ("True", "False"), ("1", "0"),
        ("Ja", "Nej"), ("ja", "nej"), ("JA", "NEJ"), ("yes", "no"),
    ];

    [GeneratedRegex(@"^\d{9,10}$")] private static partial Regex UnixSecondsRegex();
    [GeneratedRegex(@"^\d{12,13}$")] private static partial Regex UnixMillisRegex();
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$")] private static partial Regex DateTimeMinutesRegex();
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T")] private static partial Regex IsoPrefixRegex();
    [GeneratedRegex(@"^[a-z0-9]{26}$")] private static partial Regex OnePasswordIdRegex();
    [GeneratedRegex(@"^[A-Za-z2-7=\s]+$")] private static partial Regex Base32Regex();

    /// <summary>Fills the format fields of <paramref name="profile"/> from the values found; returns warnings.</summary>
    public static List<string> Detect(FormatProfile profile, IReadOnlyList<PasswordEntry> entries)
    {
        var warnings = new List<string>();

        var favorites = entries.Select(e => e.Favorite).Where(v => v.Length > 0).Distinct().ToList();
        if (favorites.Count > 0)
        {
            var pair = FavoritePairs.FirstOrDefault(p => favorites.All(v => v == p.True || v == p.False));
            if (pair == default)
            {
                profile.FavoriteUnknown = true;
                warnings.Add($"Ukendt format i kolonnen '{Columns.Favorite}' – værdierne bevares uændret.");
            }
            else
            {
                (profile.FavoriteTrue, profile.FavoriteFalse) = pair;
            }
        }

        var timestamps = entries.SelectMany(e => new[] { e.Created, e.Updated }).Where(v => v.Length > 0).ToList();
        if (timestamps.Count > 0)
        {
            var style = new[] { TimestampStyle.UnixSeconds, TimestampStyle.UnixMilliseconds, TimestampStyle.DateTimeMinutes, TimestampStyle.Iso8601 }
                .Cast<TimestampStyle?>()
                .FirstOrDefault(s => timestamps.All(v => ParseTimestamp(v, s!.Value) != null));
            profile.TimestampStyle = style ?? TimestampStyle.Unknown;
            if (style == null)
                warnings.Add($"Ukendt datoformat i '{Columns.Created}'/'{Columns.Updated}' – værdierne bevares uændret, og nye poster får tomme datoer.");
        }

        var ids = entries.Select(e => e.Id).Where(v => v.Length > 0).ToList();
        if (ids.Count > 0)
        {
            if (ids.All(v => OnePasswordIdRegex().IsMatch(v))) profile.IdStyle = IdStyle.OnePassword;
            else if (ids.All(v => Guid.TryParseExact(v, "D", out _))) profile.IdStyle = IdStyle.Guid;
            else
            {
                profile.IdStyle = IdStyle.Unknown;
                warnings.Add($"Ukendt format i kolonnen '{Columns.Id}' – nye poster får tomt Id.");
            }
        }

        var otps = entries.Select(e => e.Otp).Where(v => v.Length > 0).ToList();
        if (otps.Count > 0 && otps.All(v => !v.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase) && Base32Regex().IsMatch(v)))
            profile.OtpStyle = OtpStyle.Base32;

        return warnings;
    }

    public static DateTimeOffset? ParseTimestamp(string value, TimestampStyle style)
    {
        switch (style)
        {
            case TimestampStyle.UnixSeconds when UnixSecondsRegex().IsMatch(value):
                return DateTimeOffset.FromUnixTimeSeconds(long.Parse(value, CultureInfo.InvariantCulture));
            case TimestampStyle.UnixMilliseconds when UnixMillisRegex().IsMatch(value):
                return DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(value, CultureInfo.InvariantCulture));
            case TimestampStyle.DateTimeMinutes when DateTimeMinutesRegex().IsMatch(value):
                return DateTimeOffset.ParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            case TimestampStyle.Iso8601 when IsoPrefixRegex().IsMatch(value):
                return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;
            default:
                return null;
        }
    }
}
