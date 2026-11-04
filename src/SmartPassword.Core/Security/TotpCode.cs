using System.Text.RegularExpressions;
using System.Web;
using OtpNet;
using SmartPassword.Core.Model;

namespace SmartPassword.Core.Security;

/// <summary>TOTP codes from the OTP/2FA field, which holds either an otpauth:// URI or a plain Base32 secret (F-48).</summary>
public static partial class TotpCode
{
    public sealed record Settings(byte[] Secret, int Digits, int Period, OtpHashMode Mode);

    // Ranges Otp.NET accepts; anything outside is treated as "no valid secret" instead of throwing.
    private const int MinDigits = 6, MaxDigits = 10, MaxPeriodSeconds = 24 * 60 * 60;

    private static Settings? Valid(Settings s) => s.Secret.Length == 0 ? null : s;

    public static Settings? Parse(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return null;

        try
        {
            if (value.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(value);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var secret = query["secret"];
                if (string.IsNullOrEmpty(secret)) return null;
                var mode = (query["algorithm"] ?? "SHA1").ToUpperInvariant() switch
                {
                    "SHA256" => OtpHashMode.Sha256,
                    "SHA512" => OtpHashMode.Sha512,
                    _ => OtpHashMode.Sha1,
                };
                var digits = query["digits"] is { } ds ? (int.TryParse(ds, out var d) ? d : -1) : 6;
                var period = query["period"] is { } ps ? (int.TryParse(ps, out var p) ? p : -1) : 30;
                if (digits is < MinDigits or > MaxDigits || period is < 1 or > MaxPeriodSeconds) return null;
                return Valid(new Settings(Base32Encoding.ToBytes(CleanSecret(secret)), digits, period, mode));
            }

            return Valid(new Settings(Base32Encoding.ToBytes(CleanSecret(value)), 6, 30, OtpHashMode.Sha1));
        }
        catch (Exception e) when (e is ArgumentException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>Current code and seconds until it changes, or null if the field holds no valid secret.</summary>
    public static (string Code, int SecondsLeft)? Compute(string value, DateTime? utcNow = null)
    {
        var settings = Parse(value);
        if (settings == null) return null;
        var now = utcNow ?? DateTime.UtcNow;
        try
        {
            // Called every second from the UI; a bad field must never take the app down.
            var totp = new Totp(settings.Secret, settings.Period, settings.Mode, settings.Digits);
            return (totp.ComputeTotp(now), totp.RemainingSeconds(now));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Stores user input in the style the file uses (A-4).</summary>
    public static string Normalize(string input, OtpStyle style, string label)
    {
        input = input.Trim();
        if (input.Length == 0) return "";
        var isUri = input.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase);
        if (style == OtpStyle.Uri && !isUri)
            return $"otpauth://totp/{Uri.EscapeDataString(label)}?secret={CleanSecret(input)}";
        return input;
    }

    private static string CleanSecret(string secret) => WhitespaceRegex().Replace(secret, "").ToUpperInvariant();

    [GeneratedRegex(@"[\s-]")] private static partial Regex WhitespaceRegex();
}
