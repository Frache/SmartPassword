using System.Security.Cryptography;
using SmartPassword.Core.Updates;

namespace SmartPassword.Tests;

public sealed class UpdateTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smartpassword-update").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly Version Current = new(1, 0, 0, 0);

    private static string Release(string tag = "v1.1.0", string exeUrl = "https://github.com/Frache/SmartPassword/releases/download/v1.1.0/SmartPassword.exe",
        string hashUrl = "https://github.com/Frache/SmartPassword/releases/download/v1.1.0/SmartPassword.exe.sha256",
        long size = 100_000_000, bool prerelease = false, string page = "https://github.com/Frache/SmartPassword/releases/tag/v1.1.0") => $$"""
        {
          "tag_name": "{{tag}}", "html_url": "{{page}}", "draft": false, "prerelease": {{(prerelease ? "true" : "false")}},
          "body": "Nyt",
          "assets": [
            { "name": "SmartPassword.exe", "size": {{size}}, "browser_download_url": "{{exeUrl}}" },
            { "name": "SmartPassword.exe.sha256", "size": 84, "browser_download_url": "{{hashUrl}}" }
          ]
        }
        """;

    [Fact]
    public void Newer_release_with_both_assets_is_offered()
    {
        var update = ReleaseInfo.FindUpdate(Release(), Current);

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 1, 0, 0), update.Version);
        Assert.EndsWith("/SmartPassword.exe", update.ExeUrl.AbsolutePath);
        Assert.Equal(100_000_000, update.ExeSize);
    }

    [Theory]
    [InlineData("v1.0.0")] // same
    [InlineData("v0.9.9")] // older
    [InlineData("v1.1.0-beta")] // not a plain version
    [InlineData("latest")]
    public void Same_older_or_odd_versions_are_not_offered(string tag) =>
        Assert.Null(ReleaseInfo.FindUpdate(Release(tag: tag), Current));

    [Fact]
    public void Prerelease_is_not_offered() =>
        Assert.Null(ReleaseInfo.FindUpdate(Release(prerelease: true), Current));

    [Theory]
    [InlineData("http://github.com/Frache/SmartPassword/releases/download/v1.1.0/SmartPassword.exe")]
    [InlineData("https://evil.example/Frache/SmartPassword/releases/download/v1.1.0/SmartPassword.exe")]
    [InlineData("https://github.com/Someone/SmartPassword/releases/download/v1.1.0/SmartPassword.exe")]
    [InlineData("https://github.com:8443/Frache/SmartPassword/releases/download/v1.1.0/SmartPassword.exe")]
    public void Downloads_from_anywhere_else_are_refused(string exeUrl) =>
        Assert.Null(ReleaseInfo.FindUpdate(Release(exeUrl: exeUrl), Current));

    [Fact]
    public void Release_without_checksum_is_refused() =>
        Assert.Null(ReleaseInfo.FindUpdate(Release(hashUrl: "https://evil.example/x.sha256"), Current));

    [Theory]
    [InlineData(0)]
    [InlineData(ReleaseInfo.MaxExeSize + 1)]
    public void Unreasonable_size_is_refused(long size) =>
        Assert.Null(ReleaseInfo.FindUpdate(Release(size: size), Current));

    [Fact]
    public void Sha256_file_is_parsed()
    {
        var hex = new string('a', 64);
        Assert.Equal(32, ReleaseInfo.ParseSha256($"{hex}  SmartPassword.exe\n")!.Length);
        Assert.Equal(32, ReleaseInfo.ParseSha256(hex.ToUpperInvariant())!.Length);
        Assert.Null(ReleaseInfo.ParseSha256("not a hash"));
        Assert.Null(ReleaseInfo.ParseSha256(new string('a', 63)));
    }

    [Fact]
    public void Install_swaps_in_verified_file()
    {
        var exe = Path.Combine(_dir, "SmartPassword.exe");
        File.WriteAllText(exe, "old");
        var download = UpdateInstaller.NewPath(exe);
        File.WriteAllText(download, "new");

        UpdateInstaller.Install(exe, download, SHA256.HashData("new"u8));

        Assert.Equal("new", File.ReadAllText(exe));
        Assert.Equal("old", File.ReadAllText(UpdateInstaller.OldPath(exe)));
        Assert.False(File.Exists(download));

        UpdateInstaller.CleanUp(exe);
        Assert.False(File.Exists(UpdateInstaller.OldPath(exe)));
    }

    [Fact]
    public void Install_refuses_file_with_wrong_checksum_and_keeps_old_exe()
    {
        var exe = Path.Combine(_dir, "SmartPassword.exe");
        File.WriteAllText(exe, "old");
        var download = UpdateInstaller.NewPath(exe);
        File.WriteAllText(download, "tampered");

        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Install(exe, download, SHA256.HashData("new"u8)));

        Assert.Equal("old", File.ReadAllText(exe));
        Assert.False(File.Exists(download));
        Assert.False(File.Exists(UpdateInstaller.OldPath(exe)));
    }
}
