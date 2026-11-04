using System.Security.Cryptography;

namespace SmartPassword.Core.Updates;

/// <summary>
/// Replaces the running single-file exe with a downloaded, verified one.
/// Windows lets a running exe be renamed but not overwritten, so the current file is moved aside to
/// <c>.old</c> (deleted on the next start) and the new file takes its name.
/// </summary>
public static class UpdateInstaller
{
    public static string NewPath(string exePath) => exePath + ".new";
    public static string OldPath(string exePath) => exePath + ".old";

    /// <summary>True when the file's SHA-256 equals <paramref name="expected"/>.</summary>
    public static bool Verify(string file, byte[] expected)
    {
        using var stream = File.OpenRead(file);
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(stream), expected);
    }

    /// <summary>
    /// Verifies <paramref name="downloadedFile"/> and swaps it in for <paramref name="exePath"/>.
    /// On any failure the original exe is left in place and the download is removed.
    /// </summary>
    public static void Install(string exePath, string downloadedFile, byte[] expectedSha256)
    {
        try
        {
            if (!Verify(downloadedFile, expectedSha256))
                throw new InvalidDataException("Den hentede fil matcher ikke den forventede SHA-256 og er ikke installeret.");

            var old = OldPath(exePath);
            if (File.Exists(old)) File.Delete(old);
            File.Move(exePath, old);
            try
            {
                File.Move(downloadedFile, exePath);
            }
            catch
            {
                File.Move(old, exePath);
                throw;
            }
        }
        finally
        {
            if (File.Exists(downloadedFile)) File.Delete(downloadedFile);
        }
    }

    /// <summary>Removes leftovers from a previous update. Best effort; called at start-up.</summary>
    public static void CleanUp(string exePath)
    {
        foreach (var path in new[] { OldPath(exePath), NewPath(exePath) })
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
