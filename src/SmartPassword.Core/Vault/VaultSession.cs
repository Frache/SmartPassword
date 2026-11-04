using System.Security.Cryptography;
using System.Text;

namespace SmartPassword.Core.Vault;

/// <summary>An unlocked vault. Holds the derived key in memory until disposed (locked).</summary>
/// <remarks>
/// Protection against lost data:
/// <list type="bullet">
/// <item>Only one session per vault file on this computer (an exclusive lock file in %TEMP%).</item>
/// <item>Before saving, the file on disk must still be the one this session read or wrote last.
/// If something else changed it (another computer via OneDrive, a restore), the save is refused
/// instead of silently overwriting those changes.</item>
/// <item>Every save keeps the previous version as <c>&lt;vault&gt;.bak</c>.</item>
/// </list>
/// </remarks>
public sealed class VaultSession : IDisposable
{
    private byte[] _key;
    private byte[] _salt;
    private KdfParameters _kdf;
    private byte[]? _fingerprint;
    private FileStream? _instanceLock;

    public string FilePath { get; }
    public VaultData Data { get; private set; }

    public static string BackupPath(string vaultPath) => vaultPath + ".bak";

    private VaultSession(string filePath, VaultData data, byte[] key, byte[] salt, KdfParameters kdf, FileStream instanceLock, byte[]? fingerprint)
    {
        FilePath = filePath;
        Data = data;
        _key = key;
        _salt = salt;
        _kdf = kdf;
        _instanceLock = instanceLock;
        _fingerprint = fingerprint;
    }

    public static VaultSession Create(string path, string masterPassword, KdfParameters? kdf = null)
    {
        path = Path.GetFullPath(path);
        var instanceLock = AcquireInstanceLock(path);
        try
        {
            if (File.Exists(path))
                throw new VaultException("Der findes allerede en fil på den placering.");

            kdf ??= KdfParameters.Default;
            var salt = VaultFile.NewSalt();
            var session = new VaultSession(path, new VaultData(), VaultFile.DeriveKey(masterPassword, salt, kdf), salt, kdf, instanceLock, null);
            session.Save();
            return session;
        }
        catch
        {
            instanceLock.Dispose();
            throw;
        }
    }

    public static VaultSession Open(string path, string masterPassword)
    {
        path = Path.GetFullPath(path);
        var instanceLock = AcquireInstanceLock(path);
        try
        {
            var file = File.ReadAllBytes(path);
            var (kdf, salt) = VaultFile.ReadHeader(file);
            var key = VaultFile.DeriveKey(masterPassword, salt, kdf);
            try
            {
                return new VaultSession(path, VaultFile.Decrypt(file, key), key, salt, kdf, instanceLock, SHA256.HashData(file));
            }
            catch
            {
                CryptographicOperations.ZeroMemory(key);
                throw;
            }
        }
        catch
        {
            instanceLock.Dispose();
            throw;
        }
    }

    /// <summary>Checks a password against the open vault without changing anything (used before export, F-23).</summary>
    public bool VerifyPassword(string masterPassword)
    {
        if (_key.Length == 0) return false;
        var key = VaultFile.DeriveKey(masterPassword, _salt, _kdf);
        try
        {
            return CryptographicOperations.FixedTimeEquals(key, _key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public void Save()
    {
        ObjectDisposedException.ThrowIf(_key.Length == 0, this);
        EnsureUnchangedOnDisk();
        var content = VaultFile.Encrypt(Data, _key, _salt, _kdf);
        VaultFile.WriteAtomic(FilePath, content, BackupPath(FilePath));
        _fingerprint = SHA256.HashData(content);
    }

    public void ChangePassword(string newMasterPassword, KdfParameters? kdf = null)
    {
        ObjectDisposedException.ThrowIf(_key.Length == 0, this);
        var salt = VaultFile.NewSalt();
        var newKdf = kdf ?? _kdf;
        var key = VaultFile.DeriveKey(newMasterPassword, salt, newKdf);
        var old = (_key, _salt, _kdf);
        (_key, _salt, _kdf) = (key, salt, newKdf);
        try
        {
            Save();
        }
        catch
        {
            // Keep the session consistent with the file on disk if the save fails.
            (_key, _salt, _kdf) = old;
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
        CryptographicOperations.ZeroMemory(old._key);

        // The backup is still encrypted with the old password; don't leave it behind.
        try { File.Delete(BackupPath(FilePath)); } catch (IOException) { }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_key);
        _key = [];
        Data = new VaultData();
        _instanceLock?.Dispose();
        _instanceLock = null;
    }

    private void EnsureUnchangedOnDisk()
    {
        if (_fingerprint == null || !File.Exists(FilePath)) return;
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(FilePath)), _fingerprint))
            throw new VaultException(
                "Vault-filen er blevet ændret uden for dette vindue (fx af OneDrive eller en anden computer). " +
                "Dine seneste ændringer er ikke gemt, så de andre ændringer ikke overskrives. Lås vaulten og åbn den igen.");
    }

    /// <summary>One open session per vault file on this computer, so two windows never overwrite each other.</summary>
    private static FileStream AcquireInstanceLock(string fullPath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())))[..32];
        var directory = Path.Combine(Path.GetTempPath(), "SmartPassword");
        Directory.CreateDirectory(directory);
        try
        {
            return new FileStream(Path.Combine(directory, hash + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException e)
        {
            throw new VaultException("Vaulten er allerede åben i et andet SmartPassword-vindue.", e);
        }
    }
}
