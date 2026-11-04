using System;
using System.IO;
using System.Text.Json;

namespace SmartPassword.App.Services;

/// <summary>Non-secret preferences, stored in %APPDATA%\SmartPassword\settings.json.</summary>
public sealed class AppSettings
{
    public string? LastVaultPath { get; set; }
    public int AutoLockMinutes { get; set; } = 5;
    public int ClipboardClearSeconds { get; set; } = 20;

    /// <summary>Look for a new release on GitHub at start-up (the app's only network access).</summary>
    public bool CheckForUpdates { get; set; } = true;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartPassword", "settings.json");

    public static string DefaultVaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SmartPassword.spv");

    // The file is not protected, so values are forced into safe ranges: an edited file must not be
    // able to switch off auto-lock or clipboard clearing, or crash the app with a negative delay.
    public const int MinAutoLockMinutes = 1, MaxAutoLockMinutes = 60;
    public const int MinClipboardClearSeconds = 5, MaxClipboardClearSeconds = 120;

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            settings = new AppSettings();
        }

        settings.AutoLockMinutes = Math.Clamp(settings.AutoLockMinutes, MinAutoLockMinutes, MaxAutoLockMinutes);
        settings.ClipboardClearSeconds = Math.Clamp(settings.ClipboardClearSeconds, MinClipboardClearSeconds, MaxClipboardClearSeconds);
        return settings;
    }

    /// <summary>Best effort: failing to remember preferences must not block unlocking.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
