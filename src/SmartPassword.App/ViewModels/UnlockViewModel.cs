using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartPassword.App.Services;
using SmartPassword.Core.Vault;

namespace SmartPassword.App.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private const int MinimumMasterPasswordLength = 10;

    private readonly MainViewModel _main;
    private readonly DialogService _dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreateMode), nameof(Heading), nameof(ActionText))]
    public partial string VaultPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Strength), nameof(StrengthText))]
    public partial string Password { get; set; } = "";

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Information from the last lock, e.g. that unsaved edits were saved.</summary>
    [ObservableProperty]
    public partial string? Notice { get; set; }

    public UpdateViewModel Update => _main.Update;

    public bool IsCreateMode => !File.Exists(VaultPath);
    public string Heading => IsCreateMode ? "Opret ny vault" : "Lås vault op";
    public string ActionText => IsCreateMode ? "Opret" : "Lås op";

    /// <summary>Rough 0-4 score for the master password (F-06).</summary>
    public int Strength
    {
        get
        {
            var p = Password;
            if (p.Length == 0) return 0;
            var classes = new[] { p.Any(char.IsLower), p.Any(char.IsUpper), p.Any(char.IsDigit), p.Any(c => !char.IsLetterOrDigit(c)) }.Count(x => x);
            var score = p.Length >= 16 ? 2 : p.Length >= MinimumMasterPasswordLength ? 1 : 0;
            score += classes >= 3 ? 2 : classes >= 2 ? 1 : 0;
            return Math.Min(score, 4);
        }
    }

    public string StrengthText => Strength switch
    {
        0 => "For svagt",
        1 => "Svagt",
        2 => "Middel",
        3 => "Godt",
        _ => "Stærkt",
    };

    public UnlockViewModel(MainViewModel main, DialogService dialogs)
    {
        _main = main;
        _dialogs = dialogs;
        VaultPath = main.Settings.LastVaultPath ?? AppSettings.DefaultVaultPath;
    }

    [RelayCommand]
    private async Task BrowseExisting()
    {
        var path = await _dialogs.OpenVaultAsync();
        if (path != null) VaultPath = path;
    }

    [RelayCommand]
    private async Task BrowseNew()
    {
        var path = await _dialogs.SaveVaultAsync("SmartPassword.spv");
        if (path == null) return;
        if (File.Exists(path))
        {
            Error = "Vælg et filnavn, der ikke findes i forvejen.";
            return;
        }
        VaultPath = path;
    }

    [RelayCommand]
    private async Task Submit()
    {
        Error = null;
        if (Update.IsInstalling) return;
        if (IsCreateMode)
        {
            if (Password.Length < MinimumMasterPasswordLength)
            {
                Error = $"Master-password skal være mindst {MinimumMasterPasswordLength} tegn.";
                return;
            }
            if (Password != ConfirmPassword)
            {
                Error = "De to passwords er ikke ens.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            var path = VaultPath;
            var password = Password;
            var create = IsCreateMode;
            var session = await Task.Run(() => create ? VaultSession.Create(path, password) : VaultSession.Open(path, password));
            Password = ConfirmPassword = "";
            _main.Unlocked(session);
        }
        catch (VaultException e)
        {
            Error = e.Message;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Kunne ikke læse/skrive filen: " + e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
