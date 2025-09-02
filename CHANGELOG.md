# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [Unreleased]
- Planned: WinUI 3 GUI, secure encrypted profiles under LocalAppData, strong profile validation, MSIX packaging with App Execution Alias, post-apply actions and automatic restore. See ROADMAP.md.

## [0.5.0] - 2025-08-31
### Added
- CLI command `setprimary <\\.\\DISPLAYx|friendly>` to set primary display.
- `IDisplayConfigurator.SetPrimary(...)` implementation for Windows, preserving layout by translating all displays.
- Idempotent behavior for `DisableMonitor` (OK when already disabled).
- Guards in `DisableMonitor` to prevent disabling the last active monitor.
- Clone-aware primary validation: allow disabling a (0,0) display when multiple targets are at (0,0).
- `Result.Options` and `Result.Hint` for actionable error messages across Enable/Disable/SetPrimary.
- `SetMonitors(IEnumerable<DesiredMonitor>)` simple flow: enable desired → ensure primary → disable others.
- Extended display details (`ActiveDetails`): orientation, scaling mode, text scaling (DPI%), active vs desktop refresh rate, HDR hints, color encoding and bits per color.
- CLI `list --details|-d|-v` to show advanced monitor information.
- Per-monitor DPI awareness and `GetDpiForMonitor` integration to compute accurate text scaling percentages.
- Profile system:
  - `DesiredProfile` and `DesiredMonitorConfig` models (primary, enabled, position, resolution, Hz, orientation, text scale hint).
  - CLI: `displayctl saveprofile [name]` and `displayctl profile <name>` to save/apply JSON profiles.
  - Windows: `SetMonitors(DesiredProfile)` first enables and sets primary, then applies layout/modes.
- Resolution/position/Hz/orientation application via `ChangeDisplaySettingsEx` batched with a single global apply.
- Documentation: `ROADMAP.md`, English-only policy in `AGENTS.md`, and project `README.md`.

### Changed
- `SetPrimary` now preserves the relative layout by translating all sources so the chosen display becomes (0,0).
- `SetMonitors` pre-validates that desired displays exist and simplifies primary selection logic.
- CLI `profile <name>` now loads a JSON profile from `./profiles/<name>.json` (falls back to legacy presets only if not found).

### Fixed
- `DisableMonitor` primary check now counts active targets at (0,0) instead of sources (clone-aware).
- Corrected SHCore interop usage and explicit lambda parameter types (resolved CS0748).
- Added missing `using` directive for SHCore interop.

### Notes
- HDR/advanced color detection is best-effort (DisplayConfig advanced color info + DEVMODE fallbacks). Writing HDR or color settings is not supported.
- Active Hz vs Desktop Hz: uses best available signals (DisplayConfig path or DEVMODE); dynamic refresh features may not always show instantaneous rate.
- Text scaling is read-only; Windows lacks a supported public API to set per-monitor scaling.

## [0.1.0] - 2025-08-31
### Added
- Initial commit and contributor guide (AGENTS.md).

[Unreleased]: https://github.com/EienWolf/DisplayControl/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/EienWolf/DisplayControl/releases/tag/v0.5.0
[0.1.0]: https://github.com/EienWolf/DisplayControl/releases/tag/v0.1.0
