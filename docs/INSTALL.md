# Installing and updating SmartPassword

This guide takes you from download to a working vault, and explains how updates, backups and moving to a new PC work. The app itself is in Danish, so button names are written as they appear on screen, for example **Opdater nu**.

**Contents**

1. [Requirements](#1-requirements)
2. [Install](#2-install)
3. [First start: create your vault](#3-first-start-create-your-vault)
4. [Import from 1Password](#4-import-from-1password)
5. [Updating](#5-updating)
6. [Backups](#6-backups)
7. [Moving to a new PC or using several PCs](#7-moving-to-a-new-pc-or-using-several-pcs)
8. [Uninstall](#8-uninstall)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Requirements

- Windows 10 or 11, 64-bit.
- About 250 MB of free disk space.
- Nothing else. SmartPassword is a single file with .NET built in, and it needs no installer or administrator rights.

## 2. Install

1. Open the [latest release](https://github.com/Frache/SmartPassword/releases/latest) and download **SmartPassword.exe**.
2. Create a folder for the program. The recommended location is your own programs folder, which needs no administrator rights:

   ```
   %LOCALAPPDATA%\Programs\SmartPassword
   ```

   Paste that path into the File Explorer address bar, then create the folder if Windows says it does not exist.

   > **Avoid `C:\Program Files`.** SmartPassword updates itself by replacing its own file, and normal users cannot write there.

3. Move **SmartPassword.exe** into the folder.
4. Start it by double-clicking. To get a shortcut, right-click the file and choose **Show more options → Pin to Start**, or **Send to → Desktop (create shortcut)**.

### "Windows protected your PC"

SmartPassword is not yet signed with a paid code-signing certificate, so Microsoft Defender SmartScreen may warn you the first time:

1. Click **More info**.
2. Check that the publisher line says *Unknown publisher* and the app is *SmartPassword.exe*.
3. Click **Run anyway**.

You only see this once per downloaded file.

### Optional: verify the download

Each release also contains `SmartPassword.exe.sha256`. To check that your file is exactly the one that was published, open PowerShell in the folder and run:

```powershell
(Get-FileHash .\SmartPassword.exe -Algorithm SHA256).Hash
```

Compare the result with the content of `SmartPassword.exe.sha256` from the same release. Upper and lower case do not matter. The built-in updater does this check automatically.

## 3. First start: create your vault

Your passwords are stored in one encrypted file, the **vault** (`.spv`).

1. On the start screen, click **Ny vault…** and choose where to save it.
   - Somewhere that is backed up, for example your OneDrive folder, is a good choice. The file is encrypted, so it is safe in the cloud. See [section 7](#7-moving-to-a-new-pc-or-using-several-pcs) for what to know when you use several PCs.
2. Choose a **master password** of at least 10 characters and type it twice.
   - A passphrase of four or five random words is both strong and easy to remember.
   - **It cannot be recovered.** If you forget it, the vault cannot be opened by anyone, including you. Write it down and keep the note somewhere physically safe until you know it by heart.
3. Click **Opret**. The vault opens.

Next time you start SmartPassword, it remembers the vault, and you only type the master password and click **Lås op**.

## 4. Import from 1Password

1. In 1Password, export your vault as **CSV**. Currently, exports with Danish column names are supported (`Navn`, `Kategori`, `Brugernavn`, …).
2. In SmartPassword, click **Optag CSV…** and choose the file.
3. Check the summary and click **Optag**.
4. When asked **Slet originalfilen?**, choose **Slet fil**. The export holds all your passwords unencrypted, so do not keep it.

   Overwriting cannot reach copies in the Recycle Bin, backups, OneDrive version history or on SSD drives. If the export was ever in OneDrive or a backup, delete it there too.

## 5. Updating

### Automatic (recommended)

When SmartPassword starts, it asks GitHub whether a newer version exists. If one does, the start screen shows it:

> **Version 1.1.0 er klar.** &nbsp; [ Opdater nu ] &nbsp; [ Se hvad der er nyt ]

1. Click **Se hvad der er nyt** if you want to read the release notes.
2. Click **Opdater nu**. SmartPassword then:
   - downloads the new `SmartPassword.exe` from this project's GitHub releases,
   - checks the file against the published SHA-256 checksum and refuses it if the two don't match,
   - replaces itself and restarts.
3. Unlock your vault as usual. The version number is shown at the bottom of the start screen.

Updates are only offered on the start screen, while your vault is locked, so nothing unsaved can be lost. Your vault and settings are not touched by an update.

**What is sent:** one anonymous request to `api.github.com` that includes the app name and version, and the download when you click **Opdater nu**. Nothing from your vault ever leaves your PC.

**Turning it off:** clear **Søg efter opdateringer ved start** at the bottom of the start screen. SmartPassword then makes no network connections at all, and you update manually.

### Manual

1. Close SmartPassword.
2. Download the new **SmartPassword.exe** from the [latest release](https://github.com/Frache/SmartPassword/releases/latest).
3. Replace the old file in `%LOCALAPPDATA%\Programs\SmartPassword` with it.
4. Start SmartPassword.

> **Coming from a build made before 1.0.0?** Those builds cannot update themselves. Update once manually, and later versions will update automatically.

## 6. Backups

- Every time SmartPassword saves, the previous version is kept next to the vault as `SmartPassword.spv.bak`. It is encrypted the same way and opened with the same master password.
- That is protection against a bad save, not a backup. Also keep a copy of the `.spv` file somewhere else, for example on a USB stick or in a second cloud service, and refresh it now and then.
- A copy of the vault file is useless without the master password, so storing it in the cloud is safe.

## 7. Moving to a new PC or using several PCs

**Moving:** install SmartPassword on the new PC ([section 2](#2-install)), copy your `.spv` file across, click **Åbn eksisterende…**, choose the file and unlock it.

**Several PCs with OneDrive (or similar):** this works, with one rule. **Keep the vault unlocked on only one PC at a time, and wait until OneDrive has finished syncing before you open it on the other.**

If the file changes on disk while you have it open, for example because OneDrive brought in an edit from your other PC, SmartPassword refuses to save rather than overwrite those changes, and tells you so. Lock the vault, unlock it again to load the newest version, and redo your last change.

## 8. Uninstall

1. Delete the folder `%LOCALAPPDATA%\Programs\SmartPassword`.
2. Delete the settings folder `%APPDATA%\SmartPassword`. It contains only preferences such as the last vault location, no passwords.
3. Your vault file is wherever you saved it. Delete it only if you are sure you no longer need the passwords.

## 9. Troubleshooting

| Message or problem | What it means | What to do |
|---|---|---|
| *Forkert master-password, eller filen er beskadiget.* | The password is wrong, or the file has been damaged. | Check Caps Lock and keyboard layout. If you are sure the password is right, try the backup: copy `SmartPassword.spv.bak` to a new name ending in `.spv` and open that. |
| *Vaulten er allerede åben i et andet SmartPassword-vindue.* | SmartPassword is already running with this vault. | Switch to the other window (check the taskbar), or close it first. |
| *Vault-filen er blevet ændret uden for dette vindue…* | Something else, usually OneDrive, changed the file after you opened it. Your latest change was **not** saved. | Lock (**Ctrl+L**), unlock again, and redo the change. See [section 7](#7-moving-to-a-new-pc-or-using-several-pcs). |
| *Opdateringen mislykkedes, og intet er ændret…* | The download failed or did not match its checksum. The old version is still installed. | Try again later, or update manually ([section 5](#manual)). |
| The update button never appears | Update check is turned off, there is no newer version, there is no internet, or you are running a build made before 1.0.0. | Check **Søg efter opdateringer ved start** and the version number at the bottom of the start screen. |
| The vault locked by itself | It locks after 5 minutes without activity, when Windows locks (**Win+L**), and when the PC goes to sleep. Unsaved changes to the open entry are saved first. | Unlock again. This is intended. |
| Forgot the master password | The vault cannot be opened. There is no recovery or back door. | Restore your passwords from another source, for example a fresh 1Password export, and create a new vault. |

Found a bug? [Open an issue](https://github.com/Frache/SmartPassword/issues). Never attach real exports or vault files. Security problems: see [SECURITY.md](../SECURITY.md).
