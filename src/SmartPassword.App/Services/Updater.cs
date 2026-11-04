using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SmartPassword.Core.Updates;

namespace SmartPassword.App.Services;

/// <summary>
/// Opt-out update check against GitHub Releases and one-click install.
/// The only network traffic SmartPassword makes: an anonymous request to api.github.com, and, when the user
/// clicks Update, the download of the release exe and its SHA-256 file. No vault data is ever sent.
/// </summary>
public sealed class Updater
{
    private static readonly HttpClient Http = CreateClient();

    public static Version CurrentVersion { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0);

    public static string CurrentVersionText => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    /// <summary>The exe that would be replaced, or null when not running as a published single-file exe (e.g. from Visual Studio).</summary>
    public static string? InstalledExe
    {
        get
        {
            // Single-file apps have no assembly location; a normal build output does. That is exactly the test here.
#pragma warning disable IL3000
            if (!string.IsNullOrEmpty(typeof(Updater).Assembly.Location)) return null;
#pragma warning restore IL3000
            var path = Environment.ProcessPath;
            return path != null && Path.GetFileName(path).Equals(ReleaseInfo.ExeAssetName, StringComparison.OrdinalIgnoreCase) ? path : null;
        }
    }

    public static bool CanSelfUpdate => InstalledExe != null;

    /// <summary>Returns a newer release, or null if there is none or GitHub can't be reached.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancel = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseInfo.LatestReleaseApi);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await Http.SendAsync(request, cancel);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancel);
            return ReleaseInfo.FindUpdate(json, CurrentVersion);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Downloads, verifies and installs the update. The caller restarts the app afterwards.</summary>
    public async Task InstallAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var exe = InstalledExe ?? throw new InvalidOperationException("Automatisk opdatering virker kun i den udgivne SmartPassword.exe.");

        var hashText = await Http.GetStringAsync(update.HashUrl, cancel);
        var expected = ReleaseInfo.ParseSha256(hashText)
                       ?? throw new InvalidDataException("Releasen har en ugyldig SHA-256-fil.");

        var target = UpdateInstaller.NewPath(exe);
        using (var response = await Http.GetAsync(update.ExeUrl, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancel)) > 0)
            {
                total += read;
                if (total > ReleaseInfo.MaxExeSize)
                    throw new InvalidDataException("Den hentede fil er større end forventet.");
                await file.WriteAsync(buffer.AsMemory(0, read), cancel);
                progress?.Report((double)total / update.ExeSize);
            }
        }

        await Task.Run(() => UpdateInstaller.Install(exe, target, expected), cancel);
    }

    /// <summary>Starts the (new) exe. The caller then shuts this instance down.</summary>
    public static void StartNewVersion()
    {
        if (InstalledExe is { } exe)
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
    }

    public static void CleanUpAfterUpdate()
    {
        if (InstalledExe is { } exe) UpdateInstaller.CleanUp(exe);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false,
            UseProxy = true,
        })
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        // GitHub requires a User-Agent; it identifies only the app and version.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SmartPassword", CurrentVersionText));
        return client;
    }
}
