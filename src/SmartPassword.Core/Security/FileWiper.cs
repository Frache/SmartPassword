using System.Security.Cryptography;

namespace SmartPassword.Core.Security;

/// <summary>Best-effort removal of an unencrypted import file (F-15).</summary>
public static class FileWiper
{
    private const int ChunkSize = 64 * 1024;

    /// <summary>
    /// Overwrites the file's existing bytes in place with random data, flushes to disk and deletes it.
    /// The file is opened without truncating, so the writes land on the clusters that held the data.
    /// This cannot reach copies kept by SSD wear levelling, shadow copies, backups or cloud version history.
    /// </summary>
    public static void OverwriteAndDelete(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
        {
            var remaining = stream.Length;
            var buffer = new byte[ChunkSize];
            while (remaining > 0)
            {
                var count = (int)Math.Min(buffer.Length, remaining);
                RandomNumberGenerator.Fill(buffer.AsSpan(0, count));
                stream.Write(buffer, 0, count);
                remaining -= count;
            }
            stream.Flush(flushToDisk: true);
        }
        File.Delete(path);
    }
}
