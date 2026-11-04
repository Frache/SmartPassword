using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartPassword.Core.Updates;

/// <summary>A newer release found on GitHub, with the files needed to install it.</summary>
public sealed record UpdateInfo(Version Version, Uri PageUrl, Uri ExeUrl, long ExeSize, Uri HashUrl, string Notes);

/// <summary>
/// Reads the GitHub "latest release" API response. A release is only offered as an update when it is newer
/// than the running version and carries both <see cref="ExeAssetName"/> and <see cref="HashAssetName"/>,
/// downloaded from this repository's own release URLs.
/// </summary>
public static partial class ReleaseInfo
{
    public const string Owner = "Frache";
    public const string Repository = "SmartPassword";
    public const string ExeAssetName = "SmartPassword.exe";
    public const string HashAssetName = "SmartPassword.exe.sha256";

    /// <summary>Upper bound for the downloaded exe; anything larger is refused.</summary>
    public const long MaxExeSize = 400L * 1024 * 1024;

    public static Uri LatestReleaseApi { get; } = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");

    public static UpdateInfo? FindUpdate(string latestReleaseJson, Version current)
    {
        using var doc = JsonDocument.Parse(latestReleaseJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (Bool(root, "draft") || Bool(root, "prerelease")) return null;

        var version = ParseVersion(Str(root, "tag_name"));
        if (version == null || version <= Normalize(current)) return null;

        if (!Uri.TryCreate(Str(root, "html_url"), UriKind.Absolute, out var page) || !IsOwnReleasePage(page)) return null;

        Uri? exe = null, hash = null;
        long exeSize = 0;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = Str(asset, "name");
                if (!Uri.TryCreate(Str(asset, "browser_download_url"), UriKind.Absolute, out var url) || !IsOwnDownload(url)) continue;
                if (name == ExeAssetName)
                {
                    exe = url;
                    exeSize = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : 0;
                }
                else if (name == HashAssetName)
                {
                    hash = url;
                }
            }
        }

        if (exe == null || hash == null || exeSize <= 0 || exeSize > MaxExeSize) return null;
        return new UpdateInfo(version, page, exe, exeSize, hash, Str(root, "body"));
    }

    /// <summary>"v1.2.3" or "1.2.3" → 1.2.3.0; anything else (e.g. "v1.2-beta") → null.</summary>
    public static Version? ParseVersion(string tag)
    {
        var text = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        return VersionRegex().IsMatch(text) && Version.TryParse(text, out var v) ? Normalize(v) : null;
    }

    /// <summary>Reads the hex digest from a "sha256sum"-style file: "&lt;64 hex&gt;  SmartPassword.exe".</summary>
    public static byte[]? ParseSha256(string hashFile)
    {
        var token = hashFile.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;
        return Convert.FromHexString(token);
    }

    /// <summary>Only https downloads from this repository's own release assets are accepted.</summary>
    public static bool IsOwnDownload(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort &&
        url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        url.AbsolutePath.StartsWith($"/{Owner}/{Repository}/releases/download/", StringComparison.OrdinalIgnoreCase);

    private static bool IsOwnReleasePage(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort &&
        url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        url.AbsolutePath.StartsWith($"/{Owner}/{Repository}/releases/", StringComparison.OrdinalIgnoreCase);

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    [GeneratedRegex(@"^\d{1,5}\.\d{1,5}(\.\d{1,5}){0,2}$")]
    private static partial Regex VersionRegex();
}
