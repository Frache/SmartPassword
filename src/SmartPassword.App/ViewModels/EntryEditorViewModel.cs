using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SmartPassword.Core.Model;
using SmartPassword.Core.Security;

namespace SmartPassword.App.ViewModels;

/// <summary>Edits a copy of an entry; changes reach the vault only on save.</summary>
public partial class EntryEditorViewModel : ViewModelBase
{
    private readonly PasswordEntry _entry;
    private readonly FormatProfile _profile;

    public Guid InternalId => _entry.InternalId;
    public bool IsNew { get; }

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Category { get; set; }
    [ObservableProperty] public partial string Username { get; set; }
    [ObservableProperty] public partial string Password { get; set; }
    [ObservableProperty] public partial string Website { get; set; }
    [ObservableProperty] public partial string Otp { get; set; }
    [ObservableProperty] public partial string Notes { get; set; }
    [ObservableProperty] public partial string OtherFields { get; set; }
    [ObservableProperty] public partial string Tags { get; set; }
    [ObservableProperty] public partial bool IsFavorite { get; set; }
    [ObservableProperty] public partial bool ShowPassword { get; set; }
    [ObservableProperty] public partial bool ShowOtpSecret { get; set; }
    [ObservableProperty] public partial string? OtpCode { get; set; }
    [ObservableProperty] public partial int OtpSecondsLeft { get; set; }

    public string Id => _entry.Id;
    public string CreatedText => FormatTime(_entry.Created);
    public string UpdatedText => FormatTime(_entry.Updated);
    public bool HasOtp => OtpCode != null;

    /// <summary>True when the editor holds something that differs from the entry it was opened with.</summary>
    public bool HasChanges => !ToEntry().ContentEquals(_entry);

    public EntryEditorViewModel(PasswordEntry entry, FormatProfile profile, bool isNew)
    {
        _entry = entry;
        _profile = profile;
        IsNew = isNew;
        Name = entry.Name;
        Category = entry.Category;
        Username = entry.Username;
        Password = entry.Password;
        Website = entry.Website;
        Otp = entry.Otp;
        Notes = entry.Notes;
        OtherFields = entry.OtherFields;
        Tags = entry.Tags;
        IsFavorite = profile.IsFavorite(entry.Favorite);
        RefreshOtp();
    }

    partial void OnOtpChanged(string value) => RefreshOtp();
    partial void OnOtpCodeChanged(string? value) => OnPropertyChanged(nameof(HasOtp));

    public void RefreshOtp()
    {
        var result = TotpCode.Compute(Otp);
        OtpCode = result?.Code;
        OtpSecondsLeft = result?.SecondsLeft ?? 0;
    }

    /// <summary>Returns the edited entry. Untouched fields keep their exact original strings.</summary>
    public PasswordEntry ToEntry()
    {
        var e = _entry.Clone();
        e.Name = Name;
        e.Category = Category;
        e.Username = Username;
        e.Password = Password;
        e.Website = Website;
        e.Otp = Otp == _entry.Otp ? Otp : TotpCode.Normalize(Otp, _profile.OtpStyle, Name);
        e.Notes = Notes;
        e.OtherFields = OtherFields;
        e.Tags = Tags;
        if (IsFavorite != _profile.IsFavorite(_entry.Favorite) && !_profile.FavoriteUnknown)
            e.Favorite = _profile.FormatFavorite(IsFavorite);
        return e;
    }

    private string FormatTime(string value)
    {
        if (value.Length == 0) return "–";
        var time = _profile.ParseTimestamp(value);
        return time?.ToLocalTime().ToString("dd-MM-yyyy HH:mm") ?? value;
    }
}
