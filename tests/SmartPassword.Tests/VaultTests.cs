using System.Text;
using SmartPassword.Core.Import;
using SmartPassword.Core.Model;
using SmartPassword.Core.Security;
using SmartPassword.Core.Vault;

namespace SmartPassword.Tests;

public sealed class VaultTests : IDisposable
{
    // Low-cost parameters keep tests fast; production uses KdfParameters.Default.
    private static readonly KdfParameters Fast = new(1024, 1, 1);
    private readonly string _dir = Directory.CreateTempSubdirectory("smartpassword-tests").FullName;
    private string VaultPath => Path.Combine(_dir, "test.spv");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static byte[] SampleCsv() => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(
        "\"Navn\",\"Kategori\",\"Brugernavn\",\"Password\",\"Website\",\"OTP/2FA\",\"Noter\",\"Andre felter\",\"Tags\",\"Favorit\",\"Oprettet\",\"Opdateret\",\"Id\"\r\n" +
        "\"eksempel.dk\",\"LOGIN\",\"test@example.com\",\"Hemmelig1\",\"https://eksempel.dk\",\"\",,\"\",\"A, B \r\n\",\"false\",\"1687765200\",\"1687765200\",\"abcdefghijklmnopqrstuvwxyz\"\r\n")];

    [Fact]
    public void Create_save_and_reopen_preserves_data()
    {
        using (var session = VaultSession.Create(VaultPath, "korrekt hest batteri", Fast))
        {
            session.Data.Merge(CsvImporter.Import(SampleCsv(), "sample.csv"));
            session.Save();
        }

        using var reopened = VaultSession.Open(VaultPath, "korrekt hest batteri");
        Assert.Single(reopened.Data.Entries);
        Assert.Equal("Hemmelig1", reopened.Data.Entries[0].Password);
    }

    [Fact]
    public void Import_save_reopen_export_is_byte_identical()
    {
        var csv = SampleCsv();
        using (var session = VaultSession.Create(VaultPath, "pw", Fast))
        {
            session.Data.Merge(CsvImporter.Import(csv));
            session.Save();
        }

        using var reopened = VaultSession.Open(VaultPath, "pw");
        Assert.Equal(csv, CsvExporter.Export(reopened.Data.Profile, reopened.Data.Entries));
    }

    [Fact]
    public void Wrong_password_is_rejected()
    {
        VaultSession.Create(VaultPath, "rigtig", Fast).Dispose();

        Assert.Throws<VaultException>(() => VaultSession.Open(VaultPath, "forkert"));
    }

    [Fact]
    public void Tampered_file_is_rejected() // AT-5
    {
        VaultSession.Create(VaultPath, "pw", Fast).Dispose();
        var bytes = File.ReadAllBytes(VaultPath);
        bytes[^1] ^= 0x01;
        File.WriteAllBytes(VaultPath, bytes);

        Assert.Throws<VaultException>(() => VaultSession.Open(VaultPath, "pw"));
    }

    [Fact]
    public void Tampered_kdf_header_is_rejected()
    {
        VaultSession.Create(VaultPath, "pw", Fast).Dispose();
        var bytes = File.ReadAllBytes(VaultPath);
        bytes[9] = 2; // iterations
        File.WriteAllBytes(VaultPath, bytes);

        Assert.Throws<VaultException>(() => VaultSession.Open(VaultPath, "pw"));
    }

    [Fact]
    public void File_does_not_contain_plaintext()
    {
        using (var session = VaultSession.Create(VaultPath, "pw", Fast))
        {
            session.Data.Merge(CsvImporter.Import(SampleCsv()));
            session.Save();
        }

        var text = Encoding.UTF8.GetString(File.ReadAllBytes(VaultPath));
        Assert.DoesNotContain("Hemmelig1", text);
        Assert.DoesNotContain("eksempel", text);
        Assert.DoesNotContain("Navn", text);
    }

    [Fact]
    public void Change_password_reencrypts()
    {
        using (var session = VaultSession.Create(VaultPath, "gammel", Fast))
            session.ChangePassword("ny");

        Assert.Throws<VaultException>(() => VaultSession.Open(VaultPath, "gammel"));
        VaultSession.Open(VaultPath, "ny").Dispose();
    }

    [Fact]
    public void Verify_password()
    {
        using var session = VaultSession.Create(VaultPath, "pw", Fast);
        Assert.True(session.VerifyPassword("pw"));
        Assert.False(session.VerifyPassword("PW"));
    }

    [Fact]
    public void New_entry_uses_file_formats_and_commit_updates_timestamp() // F-49, F-4A
    {
        var data = new VaultData();
        data.Merge(CsvImporter.Import(SampleCsv()));
        var now = DateTimeOffset.FromUnixTimeSeconds(1800000000);

        var entry = data.CreateEntry(now);
        Assert.Equal("1800000000", entry.Created);
        Assert.Equal("false", entry.Favorite);
        Assert.Equal("LOGIN", entry.Category);
        Assert.Matches("^[a-z2-7]{26}$", entry.Id);

        var edit = data.Entries[0].Clone();
        edit.Password = "Ny";
        data.Commit(edit, now);
        Assert.Equal("1800000000", data.Entries[0].Updated);
        Assert.Equal("1687765200", data.Entries[0].Created);

        var unchanged = data.Entries[0].Clone();
        data.Commit(unchanged, now.AddDays(1));
        Assert.Equal("1800000000", data.Entries[0].Updated);
    }

    [Fact]
    public void Merge_skips_duplicates_by_id() // F-16
    {
        var data = new VaultData();
        data.Merge(CsvImporter.Import(SampleCsv()));

        var skipped = data.Merge(CsvImporter.Import(SampleCsv()));

        Assert.Equal(1, skipped);
        Assert.Single(data.Entries);
    }

    [Fact]
    public void Password_generator_respects_options()
    {
        var pw = PasswordGenerator.Generate(new PasswordOptions(Length: 32, Symbols: false, AvoidAmbiguous: true));

        Assert.Equal(32, pw.Length);
        Assert.Matches("^[A-Za-z0-9]+$", pw);
        Assert.DoesNotMatch("[0O1lI]", pw);
        Assert.Matches("[A-Z]", pw);
        Assert.Matches("[a-z]", pw);
        Assert.Matches("[0-9]", pw);
    }

    [Fact]
    public void Totp_matches_rfc6238_reference() // AT-3b
    {
        // RFC 6238 test secret "12345678901234567890" in Base32, 8 digits, T = 59 s => 94287082.
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        var time = DateTime.UnixEpoch.AddSeconds(59);

        Assert.Equal("94287082", TotpCode.Compute($"otpauth://totp/x?secret={secret}&digits=8", time)!.Value.Code);
        Assert.Equal("287082", TotpCode.Compute(secret, time)!.Value.Code);
        Assert.Null(TotpCode.Compute("ikke en hemmelighed!"));
    }

    [Theory]
    [InlineData("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&period=0")]
    [InlineData("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&period=-30")]
    [InlineData("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&period=abc")]
    [InlineData("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&digits=0")]
    [InlineData("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&digits=99")]
    [InlineData("otpauth://totp/x?secret=")]
    [InlineData("-")]
    public void Totp_invalid_parameters_give_no_code_instead_of_crashing(string value)
    {
        Assert.Null(TotpCode.Compute(value));
    }

    [Fact]
    public void Second_session_on_same_vault_is_refused()
    {
        using var first = VaultSession.Create(VaultPath, "pw", Fast);

        var e = Assert.Throws<VaultException>(() => VaultSession.Open(VaultPath, "pw"));
        Assert.Contains("allerede åben", e.Message);
    }

    [Fact]
    public void Vault_can_be_reopened_after_lock()
    {
        VaultSession.Create(VaultPath, "pw", Fast).Dispose();
        VaultSession.Open(VaultPath, "pw").Dispose();
        VaultSession.Open(VaultPath, "pw").Dispose();
    }

    [Fact]
    public void Save_refuses_to_overwrite_external_changes()
    {
        using var session = VaultSession.Create(VaultPath, "pw", Fast);
        var bytes = File.ReadAllBytes(VaultPath);
        bytes[^1] ^= 0x01; // e.g. OneDrive synced another computer's version
        File.WriteAllBytes(VaultPath, bytes);

        session.Data.Merge(CsvImporter.Import(SampleCsv()));
        Assert.Throws<VaultException>(session.Save);
        Assert.Equal(bytes, File.ReadAllBytes(VaultPath));
    }

    [Fact]
    public void Save_keeps_previous_version_as_backup()
    {
        using var session = VaultSession.Create(VaultPath, "pw", Fast);
        var before = File.ReadAllBytes(VaultPath);

        session.Data.Merge(CsvImporter.Import(SampleCsv()));
        session.Save();

        Assert.Equal(before, File.ReadAllBytes(VaultSession.BackupPath(VaultPath)));
        Assert.NotEqual(before, File.ReadAllBytes(VaultPath));
        Assert.False(File.Exists(VaultPath + ".tmp"));
    }

    [Fact]
    public void Change_password_removes_backup_with_old_password()
    {
        using var session = VaultSession.Create(VaultPath, "gammel", Fast);
        session.Save();
        Assert.True(File.Exists(VaultSession.BackupPath(VaultPath)));

        session.ChangePassword("ny");

        Assert.False(File.Exists(VaultSession.BackupPath(VaultPath)));
    }

    [Fact]
    public void File_wiper_overwrites_and_deletes()
    {
        var path = Path.Combine(_dir, "export.csv");
        File.WriteAllBytes(path, SampleCsv());

        FileWiper.OverwriteAndDelete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Totp_normalize_follows_file_style()
    {
        Assert.Equal("otpauth://totp/Bank?secret=JBSWY3DPEHPK3PXP", TotpCode.Normalize("jbsw y3dp ehpk 3pxp", OtpStyle.Uri, "Bank"));
        Assert.Equal("JBSWY3DPEHPK3PXP", TotpCode.Normalize("JBSWY3DPEHPK3PXP", OtpStyle.Base32, "Bank"));
    }
}
