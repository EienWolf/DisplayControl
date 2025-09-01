## Roadmap

This document outlines the path to a solid 1.0.0, current scope, and planned milestones. The project currently works for the most important use‑cases (CLI, Windows display control, profiles), and will evolve toward a polished GUI app with secure profiles and a Microsoft Store package.

### Principles
- Semantic Versioning (SemVer): MAJOR.MINOR.PATCH
- One stable public surface: CLI + Abstractions (IDisplayConfigurator + Models)
- Backward compatibility by default; explicit breaking changes only at MAJOR bumps
- Prefer Windows public APIs; avoid registry hacks or unsupported calls

### Versioning Strategy
- 0.x: fast iteration; breaking changes allowed
- 1.0.0: stable CLI/Abstractions/Profiles format + MSIX packaging ready for Store
- Pre‑releases: -alpha.N / -beta.N / -rc.N for stabilization

### Milestones

1) 0.5.0 – CLI hardening and profiles baseline
- Improve error messages, options, and hints (done for key paths)
- Add basic save/apply profiles from JSON (done)
- Implement resolution/position/frequency/orientation application (done)
- Introduce basic rollback on failure (TBD)

2) 0.6.x – GUI (WinUI 3, Windows App SDK)
- Build a WinUI 3 front‑end over Abstractions
- Display list with live refresh, primary selection, layout preview
- Apply/rollback UI flow, progress and safe‑mode

3) 0.7.x – Secure profiles
- Store profiles under %LOCALAPPDATA%\DisplayChangerX\Profiles
- Encrypt profile payloads via DPAPI (ProtectedData.CurrentUser)
- Add schemaVersion to profiles and validation layer (JSON schema)
- Migration from legacy JSON: import → encrypt → remove plaintext

4) 0.8.x – Actions & session restore
- Extend profiles with post‑apply Actions (launch apps/scripts)
- Optionally restore previous profile when the launched process exits
- CLI support: `displayctl profile <name> --wait` and return codes

5) 0.9.0-rc – Packaging & distribution
- MSIX packaging project with App Execution Alias for CLI (displayctl)
- Signing, Store listing assets, and winget manifest
- Minimal telemetry (opt‑in) and diagnostics logs

6) 1.0.0 – General Availability
- Stable CLI and Abstractions (no breaking changes)
- GUI feature‑complete: list, enable/disable, primary, layout edit, profiles
- Secure profiles with validation, migration documented
- MSIX published on Microsoft Store; README and user guide updated

### Technical Tracks
- Display APIs: User32 DisplayConfig (Get/SetDisplayConfig), ChangeDisplaySettingsEx for DEVMODE fields
- DPI/Text Scaling: SHCore GetDpiForMonitor (read); note that writing per‑monitor scaling isn’t publicly supported
- HDR/Color: DisplayConfig GET_TARGET_ADVANCED_COLOR_INFO (read); DXGI color space (future)
- Profiles: JSON model with schemaVersion, encryption via DPAPI, validation before apply
- Rollback: capture snapshot, attempt atomic apply, revert on failure/time‑boxed cancel

### Risks & Mitigations
- Driver variance: validate capabilities and short‑circuit unsupported changes
- Multi‑monitor race conditions: apply with minimal calls; prefer global apply semantics
- HDR/Color inconsistencies: expose read‑only until a robust path exists
- Text scaling write: out of scope unless a supported API becomes available

### Definition of Done for 1.0.0
- CLI + GUI stable, with documented commands and UI flows
- Profiles: encrypted at rest, validated before apply, migration documented
- Packaging: MSIX with execution alias; published to Store
- Version: v1.0.0 tag with release notes and changelog

