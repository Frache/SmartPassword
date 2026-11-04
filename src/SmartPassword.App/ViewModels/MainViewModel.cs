using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SmartPassword.App.Services;
using SmartPassword.Core.Vault;

namespace SmartPassword.App.ViewModels;

/// <summary>Switches between the unlock screen and the open vault, and handles auto-lock (F-03).</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly DialogService _dialogs;
    private readonly SecureClipboard _clipboard;
    private readonly DispatcherTimer _idleTimer;
    private DateTime _lastActivity = DateTime.UtcNow;

    public AppSettings Settings { get; }
    public UpdateViewModel Update { get; }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public MainViewModel(DialogService dialogs, SecureClipboard clipboard)
    {
        _dialogs = dialogs;
        _clipboard = clipboard;
        Settings = AppSettings.Load();
        _clipboard.ClearAfter = TimeSpan.FromSeconds(Settings.ClipboardClearSeconds);
        Update = new UpdateViewModel(Settings);
        _ = Update.CheckAsync();
        CurrentPage = new UnlockViewModel(this, _dialogs);

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _idleTimer.Tick += (_, _) =>
        {
            if (CurrentPage is VaultViewModel && DateTime.UtcNow - _lastActivity > TimeSpan.FromMinutes(Settings.AutoLockMinutes))
                Lock();
        };
        _idleTimer.Start();
    }

    public void RegisterActivity() => _lastActivity = DateTime.UtcNow;

    public void Unlocked(VaultSession session)
    {
        Settings.LastVaultPath = session.FilePath;
        Settings.Save();
        RegisterActivity();
        CurrentPage = new VaultViewModel(this, session, _dialogs, _clipboard);
    }

    public void Lock()
    {
        if (CurrentPage is not VaultViewModel vault)
            return;
        _dialogs.CloseAll();
        var notice = vault.Close();
        _ = _clipboard.ClearAsync();
        CurrentPage = new UnlockViewModel(this, _dialogs) { Notice = notice };
    }
}
