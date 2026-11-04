using SmartPassword.Core.Import;
using SmartPassword.Core.Model;

namespace SmartPassword.Core.Vault;

/// <summary>The decrypted contents of a vault file.</summary>
public sealed class VaultData
{
    public int Version { get; set; } = 1;
    public FormatProfile Profile { get; set; } = new();
    public List<PasswordEntry> Entries { get; set; } = new();
    public List<PasswordEntry> Trash { get; set; } = new();

    public PasswordEntry CreateEntry(DateTimeOffset now)
    {
        var stamp = Profile.FormatTimestamp(now);
        return new PasswordEntry
        {
            Category = Profile.DefaultCategory,
            Favorite = Profile.FormatFavorite(false),
            Created = stamp,
            Updated = stamp,
            Id = NewUniqueId(),
        };
    }

    /// <summary>Stores an edited copy; refreshes "Opdateret" when something actually changed (F-49).</summary>
    public void Commit(PasswordEntry edited, DateTimeOffset now)
    {
        var index = Entries.FindIndex(e => e.InternalId == edited.InternalId);
        if (index < 0)
        {
            Entries.Add(edited);
            return;
        }

        var existing = Entries[index];
        if (existing.ContentEquals(edited)) return;

        if (edited.Updated == existing.Updated && Profile.TimestampStyle != TimestampStyle.Unknown)
            edited.Updated = Profile.FormatTimestamp(now);
        Entries[index] = edited;
    }

    public void MoveToTrash(Guid internalId)
    {
        var entry = Entries.FirstOrDefault(e => e.InternalId == internalId);
        if (entry == null) return;
        Entries.Remove(entry);
        Trash.Add(entry);
    }

    public void Restore(Guid internalId)
    {
        var entry = Trash.FirstOrDefault(e => e.InternalId == internalId);
        if (entry == null) return;
        Trash.Remove(entry);
        Entries.Add(entry);
    }

    /// <summary>
    /// Adds imported entries. An empty vault adopts the file's format profile; otherwise the columns must match,
    /// and entries whose Id already exists are skipped (F-16). Returns the number of skipped duplicates.
    /// </summary>
    public int Merge(ImportResult import)
    {
        if (Entries.Count == 0 && Trash.Count == 0)
        {
            Profile = import.Profile;
            Entries.AddRange(import.Entries);
            return 0;
        }

        if (!Profile.Columns.SequenceEqual(import.Profile.Columns))
            throw new InvalidOperationException("Filen har andre kolonner end den, vaulten blev oprettet fra.");

        var existingIds = Entries.Select(e => e.Id).Where(id => id.Length > 0).ToHashSet();
        var skipped = 0;
        foreach (var entry in import.Entries)
        {
            if (entry.Id.Length > 0 && existingIds.Contains(entry.Id))
            {
                skipped++;
                continue;
            }
            Entries.Add(entry);
        }
        return skipped;
    }

    private string NewUniqueId()
    {
        if (Profile.IdStyle == IdStyle.Unknown) return "";
        var used = Entries.Concat(Trash).Select(e => e.Id).ToHashSet();
        string id;
        do id = Profile.NewId(); while (used.Contains(id));
        return id;
    }
}
