using SmartPassword.Core.Model;

namespace SmartPassword.Tests;

public class FieldFormatTests
{
    private static FormatProfile Detect(out List<string> warnings, params PasswordEntry[] entries)
    {
        var profile = new FormatProfile();
        warnings = FieldFormats.Detect(profile, entries);
        return profile;
    }

    [Theory]
    [InlineData("true", "false", "true", "false")]
    [InlineData("1", "0", "1", "0")]
    [InlineData("Ja", "Nej", "Ja", "Nej")]
    public void Favorite_style_is_detected(string a, string b, string expectedTrue, string expectedFalse)
    {
        var p = Detect(out var warnings, new PasswordEntry { Favorite = a }, new PasswordEntry { Favorite = b });

        Assert.Equal(expectedTrue, p.FavoriteTrue);
        Assert.Equal(expectedFalse, p.FavoriteFalse);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("1687765200", TimestampStyle.UnixSeconds)]
    [InlineData("1687765200000", TimestampStyle.UnixMilliseconds)]
    [InlineData("2023-06-26 09:40", TimestampStyle.DateTimeMinutes)]
    [InlineData("2023-06-26T09:40:00Z", TimestampStyle.Iso8601)]
    public void Timestamp_style_is_detected(string value, TimestampStyle expected)
    {
        var p = Detect(out _, new PasswordEntry { Created = value, Updated = value });

        Assert.Equal(expected, p.TimestampStyle);
        Assert.Equal(value, p.FormatTimestamp(p.ParseTimestamp(value)!.Value));
    }

    [Fact]
    public void Unknown_timestamp_gives_warning_and_empty_new_values() // AT-9, F-79
    {
        var p = Detect(out var warnings, new PasswordEntry { Created = "26. juni 2023" });

        Assert.Equal(TimestampStyle.Unknown, p.TimestampStyle);
        Assert.Single(warnings);
        Assert.Equal("", p.FormatTimestamp(DateTimeOffset.Now));
    }

    [Fact]
    public void Id_styles_are_detected_and_generated()
    {
        var onePassword = Detect(out _, new PasswordEntry { Id = "abcdefghijklmnopqrstuvwxyz" });
        var guid = Detect(out _, new PasswordEntry { Id = Guid.NewGuid().ToString() });

        Assert.Equal(IdStyle.OnePassword, onePassword.IdStyle);
        Assert.Matches("^[a-z2-7]{26}$", onePassword.NewId());
        Assert.Equal(IdStyle.Guid, guid.IdStyle);
        Assert.True(Guid.TryParse(guid.NewId(), out _));
    }

    [Fact]
    public void Otp_style_is_detected()
    {
        Assert.Equal(OtpStyle.Base32, Detect(out _, new PasswordEntry { Otp = "JBSWY3DPEHPK3PXP" }).OtpStyle);
        Assert.Equal(OtpStyle.Uri, Detect(out _, new PasswordEntry { Otp = "otpauth://totp/x?secret=JBSWY3DPEHPK3PXP" }).OtpStyle);
    }
}
