# SmartPassword – Requirements Specification

| | |
|---|---|
| **Version** | 0.5 (draft – technology chosen: C# / Avalonia) |
| **Date** | 2026-11-01 |
| **Status** | Under review |

---

## 1. Purpose

SmartPassword is a local desktop password manager. Users can **ingest** (import) an existing password file into the program, work with its contents securely and encrypted, and at any time **extract** the file again in **exactly the same format** it was ingested in. The program must never become a trap – users can always leave without losing data or structure.

## 2. Scope

**In scope (v1):**
- Local desktop application, one user per vault
- Import/export of password files, preserving the original format
- Encrypted local storage
- Search, view, create, edit and delete entries
- Password generator
- Copy to clipboard with automatic clearing

**Out of scope (v1):**
- Cloud sync and sharing between users
- Browser extension / browser autofill
- Mobile apps
- Multiple users on the same vault

## 3. Glossary

| Term | Meaning |
|---|---|
| **Vault** | The program's encrypted data file containing all entries |
| **Entry** | One login record (e.g. title, username, password, URL, notes) |
| **Master password** | The single password the user must remember to unlock the vault |
| **Ingest** | Importing an external file into the vault, including recording the file's format |
| **Extract** | Exporting the vault's contents back to the format the file was ingested in |
| **Format profile** | Stored description of the original file's structure (file type, columns, order, encoding, separator, etc.) |

---

## 4. Functional requirements

Priority: **M** = Must, **S** = Should, **C** = Could (MoSCoW).

### 4.1 Vault and access

| ID | Requirement | Pri. |
|---|---|---|
| F-01 | The user can create a new, empty vault protected by a master password. | M |
| F-02 | The user can unlock the vault with the master password. A wrong password gives a generic error without details. | M |
| F-03 | The vault locks automatically after configurable inactivity (default 5 min) and when Windows is locked / goes to sleep. | M |
| F-04 | The user can lock the vault manually (button + shortcut). | M |
| F-05 | The user can change the master password; the vault is re-encrypted. | M |
| F-06 | A strength indicator is shown when choosing a master password, and very weak passwords are rejected. | S |
| F-07 | Windows Hello as quick unlock (master password still required on first start). | C |

### 4.2 Ingest (import)

| ID | Requirement | Pri. |
|---|---|---|
| F-10 | The user can pick a file (dialog or drag-and-drop) and ingest it into the vault. | M |
| F-11 | The program automatically recognises file type and structure and shows a preview before ingesting. | M |
| F-12 | For CSV, the user can confirm/adjust the column mapping (which column is title, username, password, URL, notes). | M |
| F-13 | Columns/fields that cannot be mapped to a standard field are kept as **custom fields** – no data may be lost. | M |
| F-14 | The program stores a **format profile** for the ingested file (see 4.4). | M |
| F-15 | After a successful ingest the user is offered to securely delete the original file (overwrite before delete). Never automatic. | S |
| F-16 | Ingest can target an existing vault (merge); duplicates are flagged and the user chooses to skip, overwrite or keep both. | S |
| F-17 | If the file cannot be read, a clear error is shown and the vault is unchanged (transactional import). | M |

### 4.3 Extract (export)

| ID | Requirement | Pri. |
|---|---|---|
| F-20 | The user can extract the vault to **the same format as the original file** (same file type, columns, column order, headers, separator, quoting, encoding, BOM and line endings). | M |
| F-21 | A file that is ingested and immediately extracted without changes must be **identical in content** to the original (round-trip). Goal: byte-identical for CSV. | M |
| F-22 | Entries created in the program after ingest are included in the extract with the same structure as the original ones. | M |
| F-23 | Extracting requires re-entering the master password, and the user is warned that the file will be **unencrypted**. | M |
| F-24 | Alternatively, the user can extract to a standard format (generic CSV / JSON), regardless of how the data came in. | S |
| F-25 | The user can make an encrypted backup of the whole vault (in the program's own format). | S |

### 4.4 Format profile

The format profile is the key to "getting out again in the same format". It is stored encrypted together with the vault and contains at least:

- File type and version, if any (e.g. CSV, Bitwarden JSON)
- Encoding, BOM yes/no, line endings (CRLF/LF)
- CSV: separator, quote character, escaping rules, header row, column names and order
- Mapping between original fields and the program's fields
- Original order of entries
- Unknown/extra fields and metadata (stored unchanged)

| ID | Requirement | Pri. |
|---|---|---|
| F-30 | Each entry remembers which format profile it came from. | M |
| F-31 | The user can see which files have been ingested and when. | S |

### 4.5 Entries

| ID | Requirement | Pri. |
|---|---|---|
| F-40 | Create, edit and delete entries. Fields match the primary CSV format (section 5.1): Name, Category, Username, Password, Website, OTP/2FA, Notes, Other fields, Tags, Favourite, Created, Updated, Id. | M |
| F-41 | List of entries with quick search (Name, Username, Website, Tags, Notes). | M |
| F-42 | Passwords are hidden by default and can be revealed temporarily. | M |
| F-43 | Copy username/password to the clipboard; the clipboard is cleared after a configurable time (default 20 s). Copies are excluded from Windows clipboard history. | M |
| F-44 | Filtering by **Category** and **Tags** (e.g. side panel with categories and a tag cloud). | M |
| F-45 | Deleted entries go to a recycle bin and can be restored. | S |
| F-46 | History of previous passwords on an entry. | C |
| F-47 | **Favourite** can be toggled on an entry, and favourites can be shown separately/on top. | M |
| F-48 | If **OTP/2FA** contains a TOTP secret (Base32 or `otpauth://` URI), the program shows the current 6-digit code with a countdown and can copy it. The secret is hidden like a password. | S |
| F-49 | **Created** is set on new entries, and **Updated** is refreshed automatically on any change – both in the same date/time format as the ingested file. | M |
| F-4A | **Id** is read-only. New entries get a unique Id in the same form as the existing ones (e.g. GUID if the file uses GUIDs). Id is used for duplicate detection when merging (F-16). | M |

### 4.6 Password generator

| ID | Requirement | Pri. |
|---|---|---|
| F-50 | Generate random passwords with a choice of length and character classes (upper/lower case, digits, symbols). Cryptographically secure randomness. | M |
| F-51 | Option to avoid easily confused characters (0/O, l/1). | S |
| F-52 | Passphrase generator (words joined by a separator). | C |

### 4.7 Health check

| ID | Requirement | Pri. |
|---|---|---|
| F-60 | Overview of weak and reused passwords. | S |
| F-61 | Check against known breaches via Have I Been Pwned (k-anonymity, only a hash prefix is sent; only after the user actively opts in). | C |

---

## 5. Supported formats

### 5.1 Primary format (Must)

The primary file to ingest is a **1Password CSV export with Danish column names**:

```
"Navn","Kategori","Brugernavn","Password","Website","OTP/2FA","Noter","Andre felter","Tags","Favorit","Oprettet","Opdateret","Id"
```

Observed in a sample export:

| Property | Observation | Consequence |
|---|---|---|
| Encoding | UTF-8 **with BOM** | BOM must be preserved on extract |
| Line endings | **CRLF** | CRLF must be preserved |
| Separator | `,` | |
| Quoting | **Mixed:** most fields are quoted, but empty fields may be *unquoted* (e.g. `"",,""` where Notes is completely empty) | Quoting must be remembered **per field**, not assumed globally (F-72 revised) |
| Category | Upper case, e.g. `LOGIN` (1Password categories: `LOGIN`, `SECURE_NOTE`, `CREDIT_CARD`, `PASSWORD`, …) | Shown with a readable name, stored/extracted with the original code |
| Tags | Several tags separated by `, ` (comma + space), e.g. `Imported, Import 2023-06-26 09:40` | Tags may themselves contain spaces, colons and dates |
| Line breaks in fields | The Tags field contains a line break (and a space before it) | One entry may span several physical lines; spaces and line breaks inside fields must be preserved exactly |
| End of file | The file ended in the middle of a field (unterminated quote) – the sample was truncated | The program must reject invalid files with a clear error (F-76) |

| Column | Field in the program | Handling |
|---|---|---|
| Navn (Name) | Title | Shown in the list; required |
| Kategori (Category) | Category | 1Password category code (e.g. `LOGIN`); filtering (F-44). New entries default to `LOGIN` |
| Brugernavn (Username) | Username | Copyable |
| Password | Password | Hidden by default, copyable |
| Website | URL | Clickable – opens in the default browser |
| OTP/2FA | TOTP secret | Hidden; code display (F-48) |
| Noter (Notes) | Notes | Multi-line text – line breaks inside fields must be preserved |
| Andre felter (Other fields) | Extra fields | Stored and extracted **unchanged as raw text**; shown as readable text. Splitting into named fields awaits clarification of the format (see section 9) |
| Tags | Tags | Split on `, ` for filtering, but unchanged tags are written back character for character (including any spaces and line breaks) |
| Favorit (Favourite) | Favourite | Boolean; written back with the same values as in the file (e.g. `true`/`false`, `1`/`0`, `Ja`/`Nej`) |
| Oprettet (Created) | Created | Date/time; original format preserved (F-49) |
| Opdateret (Updated) | Updated | Date/time; original format preserved (F-49) |
| Id | Id | Read-only, unique (F-4A) |

CSV requirements for the primary format:

| ID | Requirement | Pri. |
|---|---|---|
| F-70 | The header row is recognised automatically, so the file is ingested without manual column mapping. | M |
| F-71 | Fields containing commas, quotes (`""`) and line breaks are parsed and written correctly per RFC 4180. | M |
| F-72 | Quoting is remembered **per field**: a field that was quoted is written quoted, and an empty unquoted field (`,,`) is written unquoted. New/changed fields are written quoted (the dominant pattern). | M |
| F-73 | Encoding (UTF-8 with/without BOM) and line endings (CRLF/LF) are detected and preserved; non-ASCII characters such as æ/ø/å must work. | M |
| F-74 | Entries are extracted in their original order; new entries are appended at the end. | M |
| F-75 | Empty fields stay empty (not `null`, `0` or similar) on extract. | M |
| F-76 | Invalid CSV (e.g. unterminated quote or wrong number of columns) is rejected with an error message giving the line number; nothing is ingested. | M |
| F-77 | The extracted file must be re-importable into 1Password. | S |
| F-78 | The format of Favourite, Created, Updated, Id and OTP/2FA is **detected per column** on ingest from the actual values (see assumptions 5.1.1) and stored in the format profile. New/changed values are written in the detected format. | M |
| F-79 | If a column's format cannot be recognised, it is treated as **raw text**: values are kept unchanged, new entries get an empty value, and the user gets a warning on ingest. Ingest must not fail for that reason. | M |

#### 5.1.1 Assumptions (1Password defaults)

The sample file did not contain these columns. Until a complete export has been seen, 1Password's usual formats are assumed. The detection in F-78 must handle both the primary assumption and the listed alternatives.

| # | Column | Primary assumption | Alternatives that must also be recognised | New values |
|---|---|---|---|---|
| A-1 | Favourite | `true` / `false` | `1`/`0`, `TRUE`/`FALSE`, `Ja`/`Nej`, empty = not favourite | Same spelling as in the file |
| A-2 | Created / Updated | Unix time in seconds (e.g. `1687765200`) | Unix time in milliseconds; ISO 8601 (`2023-06-26T09:40:00Z`); `yyyy-MM-dd HH:mm` | Same format and time-zone notation as the file |
| A-3 | Id | 1Password UUID: 26 characters, lower-case letters and digits (e.g. `abcdefghij0123456789klmnop`) | Standard GUID with hyphens | Random Id in the same form, guaranteed unique in the vault |
| A-4 | OTP/2FA | `otpauth://totp/...?secret=...` URI | Plain Base32 secret; empty = no 2FA | Stored as an `otpauth://` URI unless the file uses plain Base32 |

The assumptions are verified in AT-8 as soon as a complete (anonymised) export is available.

### 5.2 Other formats

| Format | Ingest | Extract in same format | Pri. |
|---|---|---|---|
| Generic CSV (arbitrary columns, `,` or `;` separator) | ✔ | ✔ | S |
| Chrome / Edge CSV export | ✔ | ✔ | C |
| Firefox CSV export | ✔ | ✔ | S |
| Bitwarden JSON / CSV (unencrypted) | ✔ | ✔ | S |
| KeePass XML (2.x) | ✔ | ✔ | S |
| Excel (.xlsx) | ✔ | ✔ | C |
| 1Password (.1pux / CSV with English column names) | ✔ | ✔ | C |

Formats are handled as plug-ins/adapters so new ones can be added without changing the core.

---

## 6. Non-functional requirements

### 6.1 Security

| ID | Requirement | Pri. |
|---|---|---|
| S-01 | The vault is encrypted with **AES-256-GCM** (or XChaCha20-Poly1305). | M |
| S-02 | The key is derived from the master password with **Argon2id** (parameters tuned to ~0.5–1 s on typical hardware) and a unique salt. | M |
| S-03 | The master password and derived key are never stored on disk and never logged. | M |
| S-04 | No network communication, except explicitly chosen features (F-61, update check). | M |
| S-05 | Sensitive data is held in memory only as long as needed and overwritten on lock, where the platform allows. | S |
| S-06 | Writing the vault is atomic (write to temp file → flush → rename), so a crash never leaves a corrupt vault. | M |
| S-07 | No sensitive data in log files, crash reports or temporary files. | M |
| S-08 | Format profile and metadata are also encrypted (not just passwords). | M |
| S-09 | Integrity check: tampering with the vault file is detected on unlock (authenticated encryption). | M |

### 6.2 Usability

| ID | Requirement | Pri. |
|---|---|---|
| U-01 | Danish user interface; English as secondary language. | M / S |
| U-02 | Full keyboard operation and shortcuts (Ctrl+F search, Ctrl+B copy username, Ctrl+Shift+C copy password (Ctrl+C is kept for normal text copying), Ctrl+L lock). | S |
| U-03 | Light and dark theme, following the Windows setting. | S |
| U-04 | A new user must be able to ingest a typical file in under 1 minute. | S |

### 6.3 Performance

| ID | Requirement | Pri. |
|---|---|---|
| P-01 | The program starts in under 2 s (excluding key derivation). | S |
| P-02 | Searching 10,000 entries responds in under 100 ms. | S |
| P-03 | Ingesting a file with 5,000 entries takes under 5 s. | S |

### 6.4 Platform and operations

| ID | Requirement | Pri. |
|---|---|---|
| D-01 | Windows 10/11 (x64). | M |
| D-02 | macOS and Linux. | C |
| D-03 | Installer as well as a portable version (runs from USB without installation). | S |
| D-04 | The user can freely choose where the vault file lives (e.g. a OneDrive folder). | M |
| D-05 | The vault file format is versioned, so newer program versions can read older vaults. | M |

---

## 7. Acceptance tests (selected)

| # | Scenario | Expected result |
|---|---|---|
| AT-1 | Ingest a file in the primary format (5.1) with 100 entries and extract immediately. | The extracted file is byte-identical to the original. |
| AT-2 | Ingest a file in the primary format where Notes contains line breaks, commas and `"`, and Name contains æ/ø/å. Extract. | All values are preserved exactly; the file is byte-identical. |
| AT-2b | Ingest a row with mixed quoting (`"",,""`) and a Tags field with `, `-separated tags and a line break. Extract. | Byte-identical; the empty field is still unquoted. |
| AT-2c | Ingest a truncated file (unterminated quote). | Rejected with an error and line number; the vault is unchanged. |
| AT-3 | Ingest a file, change one password, add one new entry, extract. | Only the changed row (Password + Updated) and the new row at the end differ; Created/Updated/Id on the new entry have the same format as the others. |
| AT-3b | Ingest an entry with a TOTP secret in OTP/2FA. | The program shows the same 6-digit code as an authenticator app. |
| AT-4 | Cut the power in the middle of a save. | The vault opens with either the previous or the new state – never corrupt. |
| AT-5 | Change one byte in the vault file and try to unlock. | Unlock is refused with an error about a damaged/tampered file. |
| AT-6 | Copy a password and wait 20 s. | The clipboard is empty, and the password does not appear in Windows clipboard history (Win+V). |
| AT-7 | Leave the program idle for 5 min. | The vault is locked and requires the master password. |
| AT-8 | Ingest a complete 1Password export, create one new entry, extract and re-import into 1Password. | Assumptions A-1–A-4 hold (or detection has adapted); the new entry has Favourite/Created/Updated/Id in the same format as the rest, and 1Password accepts the file. |
| AT-9 | Ingest a file where Created contains an unknown date format. | Ingest succeeds with a warning; the values are extracted unchanged (F-79). |

---

## 8. Technology

| Option | Pros | Cons |
|---|---|---|
| **C# / .NET + WPF or Avalonia** | Native Windows feel, strong crypto libraries, Avalonia gives cross-platform | A bit more UI boilerplate |
| **Tauri (Rust core + web UI)** | Small installer, Rust for crypto and file handling, cross-platform | Two languages in the project |
| **Electron (TypeScript)** | Fast UI development | Large installer, bigger attack surface – less suitable for a password manager |

**Decided: C# / .NET 10 (LTS) with Avalonia UI.**

Solution structure:

| Project | Contents |
|---|---|
| `SmartPassword.Core` | Domain model, CSV engine (ingest/extract + format profile), vault encryption. No UI dependencies. |
| `SmartPassword.App` | Avalonia UI (MVVM with CommunityToolkit.Mvvm). |
| `SmartPassword.Tests` | xUnit tests, including round-trip and acceptance tests from section 7. |

Libraries: `Konscious.Security.Cryptography.Argon2` (Argon2id), `System.Security.Cryptography.AesGcm` (built in), `Otp.NET` (TOTP). The CSV parser is hand-written, since byte-identical round-trip with per-field quoting (F-72) is not supported by common libraries.

---

## 9. Open questions

1. ~~Which file needs to be ingested in practice?~~ **Resolved:** CSV with header (section 5.1).
2. **Must the extract be byte-identical**, or is it enough that structure and content are the same?
2a. ~~Which program produced the CSV file?~~ **Resolved:** 1Password (Danish export).
2b. **What does "Other fields" (Andre felter) typically contain?** (E.g. `key: value` per line, JSON, or free text.) Decides whether they can be shown as separate fields.
2c. ~~Format of Favourite, Created/Updated, Id and OTP/2FA~~ **Decided:** 1Password defaults assumed (5.1.1) with automatic detection as a safety net (F-78/F-79). Verified in AT-8.
3. **Should the program support several ingested files** in the same vault (each with its own format profile), or one file per vault?
4. **Should the original file be kept in sync** (the program writes changes back continuously), or does extraction only happen when the user asks? (The spec assumes the latter, for security reasons.)
5. Windows only, or also macOS/Linux?
6. ~~Preferred programming language / technology?~~ **Decided:** C# / .NET 10 + Avalonia (section 8).
