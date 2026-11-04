using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace SmartPassword.Core.Vault;

public sealed record KdfParameters(int MemoryKiB, int Iterations, int Parallelism)
{
    public static readonly KdfParameters Default = new(64 * 1024, 3, 4);
}

public sealed class VaultException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Encrypted vault file format (spec S-01, S-02, S-06, S-08, S-09):
/// <code>
/// "SPV1" | version:u8 | memoryKiB:i32 | iterations:i32 | parallelism:i32 | salt[16] | nonce[12] | tag[16] | ciphertext
/// </code>
/// Payload is JSON of <see cref="VaultData"/>, encrypted with AES-256-GCM using an Argon2id-derived key.
/// The whole header is authenticated as associated data, so tampering with parameters is detected.
/// </summary>
public static class VaultFile
{
    private static readonly byte[] Magic = "SPV1"u8.ToArray();
    private const byte FormatVersion = 1;
    private const int SaltSize = 16, NonceSize = 12, TagSize = 16;
    private const int HeaderSize = 4 + 1 + 12 + SaltSize + NonceSize;

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public static byte[] DeriveKey(string password, byte[] salt, KdfParameters kdf)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = kdf.MemoryKiB,
                Iterations = kdf.Iterations,
                DegreeOfParallelism = kdf.Parallelism,
            };
            return argon.GetBytes(32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static byte[] Encrypt(VaultData data, byte[] key, byte[] salt, KdfParameters kdf)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(data);
        try
        {
            var result = new byte[HeaderSize + TagSize + plaintext.Length];
            var header = result.AsSpan(0, HeaderSize);
            Magic.CopyTo(header);
            header[4] = FormatVersion;
            BinaryPrimitives.WriteInt32LittleEndian(header[5..], kdf.MemoryKiB);
            BinaryPrimitives.WriteInt32LittleEndian(header[9..], kdf.Iterations);
            BinaryPrimitives.WriteInt32LittleEndian(header[13..], kdf.Parallelism);
            salt.CopyTo(header[17..]);
            RandomNumberGenerator.Fill(header.Slice(17 + SaltSize, NonceSize));

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(
                header.Slice(17 + SaltSize, NonceSize),
                plaintext,
                result.AsSpan(HeaderSize + TagSize),
                result.AsSpan(HeaderSize, TagSize),
                header);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static (KdfParameters Kdf, byte[] Salt) ReadHeader(byte[] file)
    {
        if (file.Length < HeaderSize + TagSize || !file.AsSpan(0, 4).SequenceEqual(Magic))
            throw new VaultException("Filen er ikke en SmartPassword-vault.");
        if (file[4] != FormatVersion)
            throw new VaultException($"Vault-version {file[4]} understøttes ikke af denne programversion.");

        var kdf = new KdfParameters(
            BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(5)),
            BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(9)),
            BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(13)));
        if (kdf.MemoryKiB is < 8 or > 4 * 1024 * 1024 || kdf.Iterations is < 1 or > 100 || kdf.Parallelism is < 1 or > 64)
            throw new VaultException("Vault-filen er beskadiget.");

        return (kdf, file.AsSpan(17, SaltSize).ToArray());
    }

    public static VaultData Decrypt(byte[] file, byte[] key)
    {
        ReadHeader(file);
        var header = file.AsSpan(0, HeaderSize);
        var plaintext = new byte[file.Length - HeaderSize - TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                header.Slice(17 + SaltSize, NonceSize),
                file.AsSpan(HeaderSize + TagSize),
                file.AsSpan(HeaderSize, TagSize),
                plaintext,
                header);
            return JsonSerializer.Deserialize<VaultData>(plaintext) ?? throw new VaultException("Vault-filen er beskadiget.");
        }
        catch (AuthenticationTagMismatchException e)
        {
            throw new VaultException("Forkert master-password, eller filen er beskadiget.", e);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Writes via a temp file and rename, so a crash never leaves a half-written vault (S-06).
    /// When <paramref name="backupPath"/> is given and the vault exists, the previous version is kept there.
    /// </summary>
    public static void WriteAtomic(string path, byte[] content, string? backupPath = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetFileName(path) + ".tmp");
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        if (backupPath != null && File.Exists(path))
        {
            try
            {
                File.Replace(temp, path, backupPath, ignoreMetadataErrors: true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
                // Some file systems lack ReplaceFile; fall back to copy + rename below.
            }
            File.Copy(path, backupPath, overwrite: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}
