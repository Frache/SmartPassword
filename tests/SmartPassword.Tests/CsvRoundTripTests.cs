using System.Text;
using SmartPassword.Core.Csv;
using SmartPassword.Core.Import;
using SmartPassword.Core.Model;

namespace SmartPassword.Tests;

public class CsvRoundTripTests
{
    private const string Header =
        "\"Navn\",\"Kategori\",\"Brugernavn\",\"Password\",\"Website\",\"OTP/2FA\",\"Noter\",\"Andre felter\",\"Tags\",\"Favorit\",\"Oprettet\",\"Opdateret\",\"Id\"";

    // Mirrors the structure of the real 1Password export: BOM, CRLF, an unquoted empty field (,,),
    // and a Tags field with ", " separators and an embedded line break preceded by a space.
    private static readonly string Sample =
        Header + "\r\n" +
        "\"eksempel.dk\",\"LOGIN\",\"test@example.com\",\"Hemmelig1\",\"https://eksempel.dk/login\",\"\",,\"\",\"LastPass, LastPass Import 2023-06-26 09:40 \r\n\",\"false\",\"1687765200\",\"1687765200\",\"abcdefghijklmnopqrstuvwxyz\"\r\n" +
        "\"Bank – æøå\",\"LOGIN\",\"jens\",\"pa\"\"ss,ord\",\"https://bank.dk\",\"otpauth://totp/Bank?secret=JBSWY3DPEHPK3PXP\",\"Linje 1\r\nLinje 2, med komma\",\"\",\"Økonomi\",\"true\",\"1600000000\",\"1700000000\",\"bcdefghijklmnopqrstuvwxyza\"\r\n";

    private static byte[] Bytes(string text, bool bom = true)
    {
        var body = Encoding.UTF8.GetBytes(text);
        return bom ? [0xEF, 0xBB, 0xBF, .. body] : body;
    }

    [Fact]
    public void Untouched_import_exports_byte_identical() // AT-1, AT-2, AT-2b
    {
        var input = Bytes(Sample);
        var result = CsvImporter.Import(input);

        Assert.Equal(input, CsvExporter.Export(result.Profile, result.Entries));
    }

    [Theory]
    [InlineData(true, "\r\n", true)]
    [InlineData(false, "\n", true)]
    [InlineData(true, "\r\n", false)]
    public void Bom_newline_and_trailing_newline_are_preserved(bool bom, string newline, bool trailing)
    {
        var text = Sample.Replace("\r\n", newline);
        if (!trailing) text = text[..^newline.Length];
        var input = Bytes(text, bom);

        var result = CsvImporter.Import(input);

        Assert.Equal(input, CsvExporter.Export(result.Profile, result.Entries));
    }

    [Fact]
    public void Values_are_unescaped_correctly()
    {
        var entries = CsvImporter.Import(Bytes(Sample)).Entries;

        Assert.Equal("", entries[0].Notes);
        Assert.Equal("LastPass, LastPass Import 2023-06-26 09:40 \r\n", entries[0].Tags);
        Assert.Equal("pa\"ss,ord", entries[1].Password);
        Assert.Equal("Linje 1\r\nLinje 2, med komma", entries[1].Notes);
        Assert.Equal("Bank – æøå", entries[1].Name);
        Assert.Equal(["LastPass", "LastPass Import 2023-06-26 09:40"], entries[0].TagList());
    }

    [Fact]
    public void Edit_and_add_only_changes_affected_rows() // AT-3
    {
        var input = Bytes(Sample);
        var result = CsvImporter.Import(input);
        result.Entries[1].Password = "Nyt";
        result.Entries.Add(new PasswordEntry { Name = "Ny", Category = "LOGIN", Favorite = "false" });

        var output = Encoding.UTF8.GetString(CsvExporter.Export(result.Profile, result.Entries));
        var expected = Encoding.UTF8.GetString(input)
            .Replace("\"pa\"\"ss,ord\"", "\"Nyt\"")
            + "\"Ny\",\"LOGIN\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"false\",\"\",\"\",\"\"\r\n";

        Assert.Equal(expected, output);
    }

    [Fact]
    public void Unquoted_empty_field_stays_unquoted_but_edited_field_is_quoted() // F-72
    {
        var result = CsvImporter.Import(Bytes(Sample));
        result.Entries[0].Notes = "note";

        var output = Encoding.UTF8.GetString(CsvExporter.Export(result.Profile, result.Entries));

        Assert.Contains("\"https://eksempel.dk/login\",\"\",\"note\",\"\",", output);
    }

    [Fact]
    public void Appending_to_file_without_trailing_newline_adds_separator_but_keeps_no_trailing_newline()
    {
        var text = Sample[..^2];
        var result = CsvImporter.Import(Bytes(text));
        result.Entries.Add(new PasswordEntry { Name = "Ny" });

        var output = Encoding.UTF8.GetString(CsvExporter.Export(result.Profile, result.Entries));

        Assert.Contains("\"bcdefghijklmnopqrstuvwxyza\"\r\n\"Ny\"", output);
        Assert.EndsWith("\"\"", output);
    }

    [Fact]
    public void Deleting_rows_and_reordering_works()
    {
        var result = CsvImporter.Import(Bytes(Sample));
        result.Entries.RemoveAt(0);

        var output = Encoding.UTF8.GetString(CsvExporter.Export(result.Profile, result.Entries));

        Assert.DoesNotContain("eksempel.dk", output);
        Assert.EndsWith("\"bcdefghijklmnopqrstuvwxyza\"\r\n", output);
    }

    [Fact]
    public void Truncated_file_is_rejected_with_line_number() // AT-2c, F-76
    {
        // Same shape as the real sample file: ends inside the quoted Tags field.
        var text = Header + "\r\n\"eksempel.dk\",\"LOGIN\",\"a\",\"b\",\"c\",\"\",,\"\",\"LastPass, Import \r\n";

        var ex = Assert.Throws<CsvFormatException>(() => CsvImporter.Import(Bytes(text)));
        Assert.Equal(2, ex.LineNumber);
    }

    [Fact]
    public void Wrong_field_count_is_rejected_with_line_number()
    {
        var text = Header + "\r\n\"a\",\"b\"\r\n";

        var ex = Assert.Throws<CsvFormatException>(() => CsvImporter.Import(Bytes(text)));
        Assert.Equal(2, ex.LineNumber);
    }

    [Fact]
    public void Missing_columns_are_rejected()
    {
        var ex = Assert.Throws<CsvFormatException>(() => CsvImporter.Import(Bytes("\"Navn\",\"Password\"\r\n")));
        Assert.Contains("Kategori", ex.Message);
    }

    [Fact]
    public void Invalid_utf8_is_rejected()
    {
        Assert.Throws<CsvFormatException>(() => CsvImporter.Import([.. Encoding.UTF8.GetBytes(Header + "\r\n"), 0xFF]));
    }

    [Fact]
    public void Unknown_extra_columns_survive_round_trip() // F-13
    {
        var text = Header.Replace(",\"Id\"", ",\"Id\",\"Ekstra\"") + "\r\n" +
                   "\"a\",\"LOGIN\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"false\",\"\",\"\",\"\",\"x\"\r\n";
        var input = Bytes(text);
        var result = CsvImporter.Import(input);

        Assert.Equal("x", result.Entries[0].ExtraColumns["Ekstra"]);
        Assert.Equal(input, CsvExporter.Export(result.Profile, result.Entries));
    }
}
