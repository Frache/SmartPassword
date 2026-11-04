using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace SmartPassword.App.Services;

/// <summary>File pickers and simple modal dialogs, owned by the main window.</summary>
public sealed class DialogService(Window owner)
{
    private static readonly FilePickerFileType CsvType = new("CSV-fil") { Patterns = ["*.csv"] };
    private static readonly FilePickerFileType VaultType = new("SmartPassword-vault") { Patterns = ["*.spv"] };

    private readonly List<Window> _open = new();

    /// <summary>Closes every open message/password dialog as if cancelled (used when the vault locks).</summary>
    public void CloseAll()
    {
        foreach (var dialog in _open.ToArray())
            dialog.Close(null);
        _open.Clear();
    }

    public async Task<string?> OpenCsvAsync() => await OpenAsync("Vælg CSV-fil", CsvType);
    public async Task<string?> OpenVaultAsync() => await OpenAsync("Åbn vault", VaultType);
    public async Task<string?> SaveCsvAsync(string suggestedName) => await SaveAsync("Udtræk til CSV", suggestedName, "csv", CsvType);
    public async Task<string?> SaveVaultAsync(string suggestedName) => await SaveAsync("Opret ny vault", suggestedName, "spv", VaultType);

    private async Task<string?> OpenAsync(string title, FilePickerFileType type)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [type],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> SaveAsync(string title, string suggestedName, string extension, FilePickerFileType type)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [type],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task ShowMessageAsync(string title, string message) =>
        await ShowAsync(title, message, okText: "OK", cancelText: null, password: false, destructive: false);

    public async Task<bool> ConfirmAsync(string title, string message, string okText = "OK", string cancelText = "Annuller") =>
        await ShowAsync(title, message, okText, cancelText, password: false, destructive: false) != null;

    /// <summary>Confirmation for irreversible actions: Enter chooses cancel, so the action needs a deliberate click.</summary>
    public async Task<bool> ConfirmDestructiveAsync(string title, string message, string okText, string cancelText = "Annuller") =>
        await ShowAsync(title, message, okText, cancelText, password: false, destructive: true) != null;

    public async Task<string?> PromptPasswordAsync(string title, string message) =>
        await ShowAsync(title, message, "OK", "Annuller", password: true, destructive: false);

    /// <summary>Returns the entered text (or "" without input box) on OK, null on cancel.</summary>
    private async Task<string?> ShowAsync(string title, string message, string okText, string? cancelText, bool password, bool destructive)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        var input = new TextBox { PasswordChar = '•', IsVisible = password };
        var ok = new Button { Content = okText, IsDefault = !destructive, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = cancelText, IsCancel = true, IsDefault = destructive, MinWidth = 90, IsVisible = cancelText != null, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (!destructive) ok.Classes.Add("accent");
        ok.Click += (_, _) => dialog.Close(password ? input.Text ?? "" : "");
        cancel.Click += (_, _) => dialog.Close(null);

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, ok },
                },
            },
        };
        dialog.Opened += (_, _) =>
        {
            if (password) input.Focus();
            else (destructive ? cancel : ok).Focus();
        };

        _open.Add(dialog);
        try
        {
            return await dialog.ShowDialog<string?>(owner);
        }
        finally
        {
            _open.Remove(dialog);
        }
    }
}
