# Security

SmartPassword stores passwords, so security reports get top priority.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Report them privately through GitHub's [private vulnerability reporting](https://github.com/Frache/SmartPassword/security/advisories/new). If that is not possible, contact me on [LinkedIn](https://dk.linkedin.com/in/frederik-boelskifte-jakobsen) and say it concerns a security issue; I will reply with a private way to share the details. Include:

- what the problem is and what an attacker could achieve
- steps to reproduce, or a proof of concept
- the version or commit you tested

Never include real passwords, real exports or real vault files. Create a test vault instead.

You can expect an acknowledgement within a week. Once a fix is released, you will be credited in the release notes unless you prefer otherwise.

## Supported versions

| Version | Supported |
|---|---|
| 1.0.x | Yes |

Security fixes go into `main` and are released as a new patch version. SmartPassword has not had an independent security audit.

## Security design

### Vault file

A vault is a single `.spv` file:

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | Magic `SPV1` |
| 4 | 1 | Format version (currently `1`) |
| 5 | 4 | Argon2id memory in KiB (little-endian) |
| 9 | 4 | Argon2id iterations |
| 13 | 4 | Argon2id parallelism |
| 17 | 16 | Salt (random, new on every master password change) |
| 33 | 12 | Nonce (random, new on every save) |
| 45 | 16 | AES-GCM authentication tag |
| 61 | … | Ciphertext |

- **Key derivation:** Argon2id, by default with 64 MiB of memory, 3 iterations and a parallelism of 4, producing a 256-bit key.
- **Encryption:** AES-256-GCM. The plaintext is the whole vault as JSON: entries, recycle bin, format profile and metadata. No part of your data is stored unencrypted.
- **Integrity:** The 45-byte header is authenticated as associated data. Changing any byte of the file, including the KDF parameters, makes unlocking fail.
- **Atomic writes:** The vault is written to a temporary file, flushed to disk and then renamed over the old file. The previous version is kept as `<vault>.spv.bak`, encrypted exactly like the vault. Changing the master password deletes that backup, because it is still encrypted with the old password.
- **No silent overwrites:** Only one window on a computer can have a given vault open. Before every save, SmartPassword checks that the file on disk is still the version it read or wrote last. If something else changed it, for example OneDrive syncing an edit from another computer, the save is refused rather than overwriting those changes.

### At runtime

- The master password and the derived key are never written to disk or logged.
- The key is kept in memory while the vault is unlocked and zeroed when it locks.
- Copied secrets are marked so Windows keeps them out of clipboard history and cloud clipboard. They are cleared after 20 seconds, but only if the clipboard still holds the copied value.
- The vault locks automatically after 5 minutes without keyboard or mouse activity, when Windows is locked, when the session is disconnected, when the PC goes to sleep, and on `Ctrl+L`. Open dialogs are closed on lock. Unsaved edits to the open entry are saved before the vault locks, so a newly generated password is never lost.
- **Network:** the only connections are the update check and update download, and both can be turned off with **Søg efter opdateringer ved start**. At start-up, the app sends one anonymous request to `api.github.com/repos/Frache/SmartPassword/releases/latest` with a `SmartPassword/<version>` User-Agent. When the user clicks **Opdater nu**, it downloads `SmartPassword.exe` and `SmartPassword.exe.sha256`, but only from `https://github.com/Frache/SmartPassword/releases/download/…`. The exe is installed only if its SHA-256 matches. No vault data is ever sent.
- Non-secret preferences, such as the last vault path and timeouts, are stored in `%APPDATA%\SmartPassword\settings.json`. The file is not protected, so values are forced into safe ranges on load: auto-lock 1–60 minutes and clipboard clearing 5–120 seconds. Editing the file cannot switch either of them off.
- After import, the unencrypted CSV can be overwritten in place with random data and then deleted.

### Threat model

SmartPassword is designed to protect your passwords if someone gets hold of the **vault file**, for example from a stolen laptop, a cloud folder or a backup.

It **cannot** protect you against:

- **A weak master password.** Argon2id slows guessing down, but cannot stop it. Use a long passphrase.
- **Malware on your computer** while the vault is unlocked, such as keyloggers, screen capture or memory readers.
- **Data left in memory.** .NET strings cannot be reliably wiped, so decrypted values may remain in process memory until the garbage collector reclaims it.
- **Exported files.** A CSV export is unencrypted by design. Delete it as soon as you no longer need it.
- **A compromised release.** The update and its checksum come from the same GitHub release. The checksum catches corrupted or tampered downloads, but not a malicious release published by someone who has taken over the GitHub account or the build pipeline. Releases are not yet code-signed.
- **Secure deletion on SSDs and cloud folders.** Overwriting the original import file does not guarantee that the data is gone. SSD wear levelling, OneDrive version history and backups may keep copies.

There is no way to recover a vault if the master password is forgotten.
