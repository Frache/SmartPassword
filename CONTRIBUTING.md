# Contributing to SmartPassword

Thank you for helping out! Bug reports, ideas and pull requests are all welcome.

## Ground rules

- **Never share real password data.** Do not attach real CSV exports, vault files or screenshots that show real entries to issues or pull requests. Use made-up test data.
- **Security problems** go through [private vulnerability reporting](SECURITY.md), not public issues.
- For larger changes, open an issue first, so we can agree on the approach before you spend time on it.

## Development setup

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows 10 or 11.

```bash
git clone https://github.com/Frache/SmartPassword.git
cd SmartPassword
dotnet build
dotnet test
dotnet run --project src/SmartPassword.App
```

## How the code is organised

- **`SmartPassword.Core`** contains all logic: the CSV parser and writer, the format profile, field-format detection and vault encryption. It must not depend on any UI library.
- **`SmartPassword.App`** is the Avalonia UI, built as MVVM with CommunityToolkit.Mvvm. Keep logic out of the views.
- **`SmartPassword.Tests`** holds xUnit tests for Core.

The requirements live in [docs/SPECIFICATION.md](docs/SPECIFICATION.md). Requirement IDs such as `F-21` or `S-06` are referenced in code comments. Please keep that habit when you implement a requirement.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request focused on one change.
3. Add or update tests. Anything that touches import or export needs a **round-trip test** proving that unchanged data still comes out byte-identical.
4. Make sure `dotnet build` produces no warnings and `dotnet test` passes.
5. Describe what changed and why, and link the issue if there is one.

## Security-sensitive code

Changes to `Vault/`, `Security/` or the clipboard handling get extra review. In particular:

- Use only `System.Security.Cryptography` primitives or well-established libraries. Do not implement your own cryptography.
- Never log or persist secrets. The update check (`Services/Updater.cs`) is the only network access. Do not add others.
- Changes to the vault file format must bump the format version and keep older vaults readable.

## License

By contributing, you agree that your contributions are licensed under the [Apache License 2.0](LICENSE).
