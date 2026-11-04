using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartPassword.App.Services;
using SmartPassword.Core.Csv;
using SmartPassword.Core.Import;
using SmartPassword.Core.Model;
using SmartPassword.Core.Security;
using SmartPassword.Core.Vault;

namespace SmartPassword.App.ViewModels;

public sealed record EntryListItem(PasswordEntry Entry, bool IsFavorite)
{
    public string Name => Entry.Name.Length > 0 ? Entry.Name : "(uden navn)";
    public string Subtitle => Entry.Username;
}

public partial class VaultViewModel : ViewModelBase
{
    private const string AllCategories = "Alle kategorier";

    private readonly MainViewModel _main;
    private readonly VaultSession _session;
    private readonly DialogService _dialogs;
    private readonly SecureClipboard _clipboard;
    private readonly DispatcherTimer _otpTimer;

    private VaultData Data => _session.Data;

    public ObservableCollection<EntryListItem> Entries { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string SelectedCategory { get; set; } = AllCategories;
    [ObservableProperty] public partial bool FavoritesOnly { get; set; }
    [ObservableProperty] public partial EntryListItem? SelectedItem { get; set; }
    [ObservableProperty] public partial EntryEditorViewModel? Editor { get; set; }
    [ObservableProperty] public partial string? Status { get; set; }

    [ObservableProperty] public partial int GeneratorLength { get; set; } = 20;
    [ObservableProperty] public partial bool GeneratorSymbols { get; set; } = true;
    [ObservableProperty] public partial bool GeneratorAvoidAmbiguous { get; set; } = true;

    public string Title => Path.GetFileName(_session.FilePath);
    public string CountText => $"{Entries.Count} af {Data.Entries.Count} poster";

    public VaultViewModel(MainViewModel main, VaultSession session, DialogService dialogs, SecureClipboard clipboard)
    {
        _main = main;
        _session = session;
        _dialogs = dialogs;
        _clipboard = clipboard;
        RefreshList();

        _otpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _otpTimer.Tick += (_, _) => Editor?.RefreshOtp();
        _otpTimer.Start();
    }

    private bool _closed;

    /// <summary>
    /// Locks the vault. Unsaved edits are saved first, so a generated password that was already
    /// set on a website is never lost to auto-lock. Returns a notice for the unlock screen, if any.
    /// </summary>
    public string? Close()
    {
        if (_closed) return null;
        _closed = true;
        _otpTimer.Stop();

        string? notice = null;
        if (Editor is { HasChanges: true } editor)
        {
            var name = editor.Name.Length > 0 ? editor.Name : "(uden navn)";
            Data.Commit(editor.ToEntry(), DateTimeOffset.Now);
            notice = SaveVault()
                ? $"Ugemte ændringer i '{name}' blev gemt, da vaulten blev låst."
                : $"Ugemte ændringer i '{name}' kunne ikke gemmes, da vaulten blev låst. {Status}";
        }

        Editor = null;
        Entries.Clear();
        _session.Dispose();
        return notice;
    }

    partial void OnSearchTextChanged(string value) => RefreshList();
    partial void OnSelectedCategoryChanged(string value) => RefreshList();
    partial void OnFavoritesOnlyChanged(bool value) => RefreshList();

    partial void OnSelectedItemChanged(EntryListItem? value)
    {
        if (value == null) return;
        if (Editor != null && Editor.InternalId == value.Entry.InternalId) return;
        Editor = new EntryEditorViewModel(value.Entry.Clone(), Data.Profile, isNew: false);
    }

    private void RefreshList(Guid? select = null)
    {
        var selectedId = select ?? SelectedItem?.Entry.InternalId;

        var categories = Data.Entries.Select(e => e.Category).Where(c => c.Length > 0).Distinct().Order().ToList();
        if (!Categories.Skip(1).SequenceEqual(categories))
        {
            var current = SelectedCategory;
            Categories.Clear();
            Categories.Add(AllCategories);
            foreach (var c in categories) Categories.Add(c);
            SelectedCategory = Categories.Contains(current) ? current : AllCategories;
        }

        var search = SearchText.Trim();
        var items = Data.Entries
            .Where(e => SelectedCategory == AllCategories || e.Category == SelectedCategory)
            .Where(e => !FavoritesOnly || Data.Profile.IsFavorite(e.Favorite))
            .Where(e => search.Length == 0 || Matches(e, search))
            .Select(e => new EntryListItem(e, Data.Profile.IsFavorite(e.Favorite)))
            .OrderByDescending(i => i.IsFavorite)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Entries.Clear();
        foreach (var item in items) Entries.Add(item);
        SelectedItem = Entries.FirstOrDefault(i => i.Entry.InternalId == selectedId);
        OnPropertyChanged(nameof(CountText));
    }

    private static bool Matches(PasswordEntry e, string search) =>
        new[] { e.Name, e.Username, e.Website, e.Tags, e.Notes }
            .Any(v => v.Contains(search, StringComparison.CurrentCultureIgnoreCase));

    private bool SaveVault()
    {
        try
        {
            _session.Save();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or VaultException)
        {
            Status = "Kunne ikke gemme vault: " + e.Message;
            return false;
        }
    }

    [RelayCommand]
    private void NewEntry()
    {
        SelectedItem = null;
        Editor = new EntryEditorViewModel(Data.CreateEntry(DateTimeOffset.Now), Data.Profile, isNew: true);
    }

    [RelayCommand]
    private void SaveEntry()
    {
        if (Editor == null) return;
        var edited = Editor.ToEntry();
        Data.Commit(edited, DateTimeOffset.Now);
        if (!SaveVault()) return;

        Editor = new EntryEditorViewModel(edited.Clone(), Data.Profile, isNew: false);
        RefreshList(edited.InternalId);
        Status = "Gemt.";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        var current = SelectedItem;
        Editor = current == null ? null : new EntryEditorViewModel(current.Entry.Clone(), Data.Profile, isNew: false);
    }

    [RelayCommand]
    private async Task DeleteEntry()
    {
        if (Editor == null) return;
        if (Editor.IsNew)
        {
            Editor = null;
            return;
        }
        var id = Editor.InternalId;
        if (!await _dialogs.ConfirmDestructiveAsync("Slet post", $"Flyt '{Editor.Name}' til papirkurven?", "Slet") || _closed) return;

        Data.MoveToTrash(id);
        SaveVault();
        Editor = null;
        RefreshList();
        Status = "Posten er lagt i papirkurven.";
    }

    [RelayCommand]
    private async Task CopyUsername()
    {
        if (Editor == null) return;
        Status = "Brugernavn kopieret.";
        await _clipboard.CopyAsync(Editor.Username);
    }

    [RelayCommand]
    private async Task CopyPassword()
    {
        if (Editor == null) return;
        Status = $"Password kopieret – ryddes om {_clipboard.ClearAfter.TotalSeconds:0} sek.";
        await _clipboard.CopyAsync(Editor.Password);
    }

    [RelayCommand]
    private async Task CopyOtp()
    {
        if (Editor?.OtpCode == null) return;
        Status = "2FA-kode kopieret.";
        await _clipboard.CopyAsync(Editor.OtpCode);
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        if (Editor == null) return;
        Editor.Password = PasswordGenerator.Generate(new PasswordOptions(
            Length: Math.Clamp(GeneratorLength, 8, 128),
            Symbols: GeneratorSymbols,
            AvoidAmbiguous: GeneratorAvoidAmbiguous));
        Editor.ShowPassword = true;
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        if (Editor == null) return;
        if (Uri.TryCreate(Editor.Website, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    [RelayCommand]
    private void Lock() => _main.Lock();

    [RelayCommand]
    private async Task Import()
    {
        // Every await below may return after the vault was locked (auto-lock, Ctrl+L, Windows lock);
        // the session is then disposed, so each step checks _closed before touching vault data.
        var path = await _dialogs.OpenCsvAsync();
        if (path == null || _closed) return;

        ImportResult result;
        try
        {
            result = CsvImporter.Import(await File.ReadAllBytesAsync(path), Path.GetFileName(path));
        }
        catch (CsvFormatException e)
        {
            await _dialogs.ShowMessageAsync("Kan ikke optage filen", e.Message + "\n\nIntet er ændret i vaulten.");
            return;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Kan ikke læse filen", e.Message);
            return;
        }

        var summary = $"Fundet {result.Entries.Count} poster i {Path.GetFileName(path)}.";
        if (result.Warnings.Count > 0)
            summary += "\n\nAdvarsler:\n• " + string.Join("\n• ", result.Warnings);
        if (!await _dialogs.ConfirmAsync("Optag fil", summary + "\n\nVil du optage filen i vaulten?", "Optag") || _closed) return;

        int skipped;
        try
        {
            skipped = Data.Merge(result);
        }
        catch (InvalidOperationException e)
        {
            await _dialogs.ShowMessageAsync("Kan ikke optage filen", e.Message);
            return;
        }
        if (!SaveVault()) return;
        RefreshList();
        Status = $"{result.Entries.Count - skipped} poster optaget" + (skipped > 0 ? $", {skipped} dubletter sprunget over." : ".");

        // F-15: offer to remove the unencrypted original, never automatically.
        if (await _dialogs.ConfirmDestructiveAsync("Slet originalfilen?",
                $"Filen '{Path.GetFileName(path)}' indeholder dine passwords ukrypteret.\n\n" +
                "Vil du slette den nu? Den overskrives med tilfældige data før sletning. " +
                "(På SSD-diske, i papirkurven, i backups og i OneDrive-versionshistorik kan kopier stadig findes.)",
                "Slet fil", "Behold"))
        {
            try
            {
                await Task.Run(() => FileWiper.OverwriteAndDelete(path));
                Status += " Originalfilen er slettet.";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                await _dialogs.ShowMessageAsync("Kunne ikke slette filen", e.Message);
            }
        }
    }

    [RelayCommand]
    private async Task Export()
    {
        var password = await _dialogs.PromptPasswordAsync("Udtræk til CSV", "Indtast master-password for at udtrække:");
        if (password == null || _closed) return;
        if (!await Task.Run(() => _session.VerifyPassword(password)) || _closed)
        {
            if (!_closed) await _dialogs.ShowMessageAsync("Udtræk til CSV", "Forkert master-password.");
            return;
        }

        if (!await _dialogs.ConfirmDestructiveAsync("Advarsel",
                "Den udtrukne fil bliver UKRYPTERET, og alle kan læse dine passwords, hvis de får fat i den.\n\nFortsæt?",
                "Udtræk") || _closed) return;

        var suggested = Data.Profile.SourceFileName.Length > 0 ? Data.Profile.SourceFileName : "SmartPassword-udtræk.csv";
        var path = await _dialogs.SaveCsvAsync(suggested);
        if (path == null || _closed) return;

        try
        {
            await File.WriteAllBytesAsync(path, CsvExporter.Export(Data.Profile, Data.Entries));
            Status = $"Udtrukket {Data.Entries.Count} poster til {Path.GetFileName(path)}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Kunne ikke skrive filen", e.Message);
        }
    }
}
