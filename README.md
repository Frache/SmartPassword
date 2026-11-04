<div align="center">

# SmartPassword

**A local, offline password manager that takes your 1Password export in and gives it back unchanged.**

[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg)](#requirements)
[![Version: 1.0](https://img.shields.io/badge/version-1.0-brightgreen.svg)](#project-status)
[![Release](https://github.com/Frache/SmartPassword/actions/workflows/release.yml/badge.svg)](https://github.com/Frache/SmartPassword/actions/workflows/release.yml)

</div>

<!-- Add a screenshot here once the UI is final, e.g. docs/images/screenshot.png -->

---

## Why SmartPassword?

Most password managers can import a CSV file, but few can give it back to you exactly as it was. SmartPassword is built around one promise:

> **You can always leave.** Import a file, use it for as long as you like, and export it again in the *same* format – byte for byte if nothing has changed.

Everything stays on your own machine in a single encrypted file. No account, no cloud, no telemetry.

## Features

### 1Password import and export
- **Ingest a 1Password CSV export** directly. The column layout is recognised automatically, so there is no manual mapping.
- **Lossless round-trip.** Encoding, BOM, line endings, column order, and even per-field quoting are remembered. An unchanged vault exports to a byte-identical file.
- **Format detection** for Favourite, Created/Updated timestamps, Id and OTP values. New entries are written in the same format as the rest of the file, so the export can be imported back into 1Password.
- **Merging.** Import more files into an existing vault; entries whose Id is already present are skipped.
- **Clean-up.** After import, SmartPassword offers to overwrite the unencrypted original in place and then delete it. It never happens automatically. On SSDs, and in backups or OneDrive version history, copies may survive.
- **Invalid files are rejected** with the offending line number, and the vault is left untouched.

> Currently supports 1Password exports with **Danish** column names (`Navn`, `Kategori`, `Brugernavn`, …). More formats are on the [roadmap](#roadmap).

### Everyday use
- Search across name, username, website, tags and notes
- Filter by category and show favourites only
- Built-in **TOTP / 2FA codes** from `otpauth://` URIs or Base32 secrets
- **Password generator** using a cryptographically secure random source, with an option to avoid look-alike characters
- Deleted entries go to a recycle bin inside the vault
- Light and dark theme following Windows

### Security
- **AES-256-GCM** authenticated encryption, with the key derived by **Argon2id**
- The whole vault is encrypted, including metadata and the format profile; tampering is detected on unlock
- **Atomic saves** (temp file → flush → rename) with a backup of the previous version, so a crash never leaves a half-written vault
- **No silent overwrites:** a vault can only be open in one window, and a save is refused if the file was changed elsewhere (e.g. by OneDrive)
- **Clipboard protection:** copied secrets are cleared after 20 seconds and excluded from Windows clipboard history and cloud clipboard
- **Auto-lock** after 5 minutes of inactivity, and whenever Windows locks or goes to sleep
- **No telemetry.** The only network access is an optional update check against GitHub Releases. It can be turned off on the start screen, and updates are verified by SHA-256 before they are installed.

See [SECURITY.md](SECURITY.md) for the vault format and the threat model.

## Requirements

- Windows 10 or 11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source

## Installation

1. Download **SmartPassword.exe** from the [latest release](https://github.com/Frache/SmartPassword/releases/latest).
2. Put it in `%LOCALAPPDATA%\Programs\SmartPassword` and start it.
3. Later versions are offered on the start screen. Click **Opdater nu** to install them.

**📖 [Full installation and update guide](docs/INSTALL.md)** covers the SmartScreen warning, verifying the download, backups, using several PCs and troubleshooting.

To build from source instead:

```bash
git clone https://github.com/Frache/SmartPassword.git
cd SmartPassword
dotnet run --project src/SmartPassword.App
```

To build a self-contained executable:

```bash
dotnet publish src/SmartPassword.App -c Release -r win-x64 --self-contained
```

## Getting started

1. **Create a vault.** Choose where to store it, for example a OneDrive folder, and pick a master password of at least 10 characters. There is no way to recover a forgotten master password.
2. **Import your passwords.** In 1Password, export your vault as CSV. In SmartPassword, choose *Import* and select the file.
3. **Delete the export.** Accept the offer to securely delete the unencrypted CSV file.
4. **Export when you need to.** *Export* asks for your master password again and writes a CSV file in the original format.

### Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+F` | Search |
| `Ctrl+N` | New entry |
| `Ctrl+S` | Save entry |
| `Ctrl+B` | Copy username |
| `Ctrl+Shift+C` | Copy password |
| `Ctrl+L` | Lock vault |

## Project structure

```
src/
  SmartPassword.Core/   Domain model, CSV engine, format profile, vault encryption (no UI dependencies)
  SmartPassword.App/    Avalonia desktop UI (MVVM with CommunityToolkit.Mvvm)
tests/
  SmartPassword.Tests/  xUnit tests: CSV round-trip, format detection, vault encryption
docs/
  INSTALL.md            Installation and update guide
  SPECIFICATION.md      Full requirements specification
  release-notes.md      Text used for the next GitHub release
.github/workflows/
  release.yml           Builds, tests and publishes a release when a vX.Y.Z tag is pushed
```

### Making a release

1. Bump `<Version>` in `src/SmartPassword.App/SmartPassword.App.csproj` and describe the changes in `docs/release-notes.md`.
2. Commit, then tag and push:

   ```bash
   git tag v1.1.0
   ```

   ```bash
   git push origin main v1.1.0
   ```

3. GitHub Actions runs the tests, builds `SmartPassword.exe` and `SmartPassword.exe.sha256`, and publishes the release. Installed copies offer it on their next start.

Run the tests with:

```bash
dotnet test
```

## Project status

SmartPassword **1.0** is the first public release. The vault file format (version 1) is stable: future versions will keep reading 1.0 vaults. As with any password manager, keep a backup of your vault file. A forgotten master password cannot be recovered.

SmartPassword has not had an independent security audit. Read the [threat model](SECURITY.md#threat-model) before relying on it.

## Roadmap

- [ ] Code-signed releases (removes the SmartScreen warning)
- [ ] Settings screen (auto-lock timeout, clipboard timeout)
- [ ] Change master password from the UI
- [ ] Restore entries from the recycle bin
- [ ] Weak and reused password report
- [ ] More import formats: generic CSV, Bitwarden, KeePass XML, Firefox, Chrome/Edge
- [ ] English user interface

The full list of requirements is in [docs/SPECIFICATION.md](docs/SPECIFICATION.md).

## Contributing

Bug reports and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) first. **Never attach real password exports or vault files to an issue.**

## Contact

Questions, feedback or collaboration: reach me on **[LinkedIn](https://dk.linkedin.com/in/frederik-boelskifte-jakobsen)**. Security issues: see [SECURITY.md](SECURITY.md).

## License

SmartPassword is licensed under the [Apache License 2.0](LICENSE). Copyright © 2026 Frederik Boelskifte Jakobsen.

SmartPassword is an independent project and is not affiliated with or endorsed by 1Password (AgileBits Inc.).
