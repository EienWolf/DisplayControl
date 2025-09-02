# Repository Guidelines

IMPORTANT: Language Policy
- Effective immediately, all new code, documentation, filenames, identifiers, and configuration must be written in English. Prefer clear, descriptive English names for variables, methods, types, and files. Update legacy Spanish identifiers opportunistically when touching related code.

## Project Structure & Modules
- `ConsoleApp.sln`: Solution entry.
- `src/DisplayControl.Abstractions`: Interfaces and models (e.g., `IDisplayConfigurator`, `DisplayInfo`).
- `src/DisplayControl.Windows`: Windows interop/services (`User32` DisplayConfig bindings, helpers).
- `src/DisplayControl.Cli`: Console entry point (`Program.cs`).
- `bin/`, `obj/`: Build outputs (ignored in VCS).

## Build, Test, and Development
- Build: `dotnet build ConsoleApp.sln`
- Run CLI: `dotnet run --project src/DisplayControl.Cli -- list`
  - More: `enable <friendly>`, `disable <\\.\\DISPLAYx|friendly>`, `profile <work|all|tv>`
- Restore: `dotnet restore`
- Publish (Windows x64): `dotnet publish src/DisplayControl.Cli -c Release -r win-x64 --self-contained false`

## Coding Style & Naming Conventions
- Language: C# targeting `net8.0` / `net8.0-windows`; nullable + implicit usings enabled.
- Indentation: 4 spaces; braces on new lines.
- Naming: PascalCase for public members/types; camelCase for locals/params.
- Files: One public type per file; file name matches type name.
- Data: Prefer `record` for immutable DTOs under `Models/`.
- Formatting: Run `dotnet format` prior to PRs.

## Documentation & Comments
- XML summaries: All public and private classes and methods must have concise XML `<summary>` documentation. Prefer 1–3 sentences focusing on intent and behavior, not implementation details.
- Interop docs: For any Win32/Windows interop (P/Invoke types, structs, enums, and methods), include a short summary and a remarks section with a link to the official Microsoft Docs page for that API.
- Inline comments: Keep inline comments to a minimum. Only add them when the action is non-obvious or complex. Do not restate what the code already conveys.
- Commented-out code: Do not keep commented-out code. Remove it instead. Use version control history if you need to recover old code.
- Language: All documentation and comments must be in English.

Internationalization & English Only
- Public strings (messages, CLI help) should be English by default. If localization is needed, introduce resource files; do not hardcode mixed languages.

## Testing Guidelines
- Framework: xUnit recommended; tests under `tests/DisplayControl.*.Tests`.
- Naming: `ClassUnderTest_Method_ExpectedBehavior` (e.g., `WindowsDisplayConfigurator_List_ReturnsActive`).
- Run: `dotnet test`
- Coverage: Target >= 80% for core logic in `Abstractions` and `Windows` services.

## Commit & Pull Request Guidelines
- Commits: Imperative subject (<= 72 chars) explaining what/why; optional scopes `[cli]`, `[windows]`, `[abstractions]`.
- Git Flow: Use `develop` and `master` as primary branches. Create `feature/<name>` from `develop`, `release/<version>` from `develop`, and `hotfix/<version>` from `master`. Tag releases as `vX.Y.Z`.
- PR base: Target `develop` for features; `master` only for `release/*` and `hotfix/*`. After a hotfix to `master`, back-merge into `develop`.
- PR content: Clear description, linked issues, and sample CLI output (e.g., `displayctl list`). CI basics pass: `dotnet build` and `dotnet format` clean.

## Security & Configuration Tips
- Platform: Windows-only APIs; develop/run on Windows 10/11.
- Permissions: Display reconfiguration can blank screens briefly—avoid during screen-sharing.
- Config: No secrets required. If adding settings, prefer `appsettings.Development.json` with documented defaults.
