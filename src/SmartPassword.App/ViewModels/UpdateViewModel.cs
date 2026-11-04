using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartPassword.App.Services;
using SmartPassword.Core.Updates;

namespace SmartPassword.App.ViewModels;

/// <summary>Update check and one-click install, shown on the unlock screen (so no vault is open while updating).</summary>
public partial class UpdateViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Updater _updater = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable), nameof(AvailableText))]
    public partial UpdateInfo? Available { get; set; }

    [ObservableProperty] public partial bool IsInstalling { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string? Message { get; set; }

    public bool IsAvailable => Available != null;
    public string AvailableText => Available == null ? "" : $"Version {Format(Available.Version)} er klar.";
    public string CurrentVersionText => $"Version {Updater.CurrentVersionText}";
    public bool CanSelfUpdate => Updater.CanSelfUpdate;

    public bool CheckAutomatically
    {
        get => _settings.CheckForUpdates;
        set
        {
            if (_settings.CheckForUpdates == value) return;
            _settings.CheckForUpdates = value;
            _settings.Save();
            OnPropertyChanged();
            if (value) _ = CheckAsync();
            else Available = null;
        }
    }

    public UpdateViewModel(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task CheckAsync()
    {
        if (!_settings.CheckForUpdates || !Updater.CanSelfUpdate) return;
        Available = await _updater.CheckAsync();
    }

    [RelayCommand]
    private async Task Install()
    {
        if (Available is not { } update || IsInstalling) return;
        IsInstalling = true;
        Message = "Henter opdatering…";
        try
        {
            await _updater.InstallAsync(update, new Progress<double>(p => Progress = Math.Clamp(p, 0, 1) * 100));
            Message = "Genstarter…";
            Updater.StartNewVersion();
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or TaskCanceledException)
        {
            Message = $"Opdateringen mislykkedes, og intet er ændret: {e.Message} Du kan hente den manuelt fra GitHub.";
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        var url = Available?.PageUrl ?? new Uri($"https://github.com/{ReleaseInfo.Owner}/{ReleaseInfo.Repository}/releases/latest");
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }

    private static string Format(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";
}
