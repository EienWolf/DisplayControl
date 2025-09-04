# Display Control (CLI + Windows Service)

A Windows-focused tool to inspect and control multi-monitor setups from a command-line interface, with an extensible abstraction layer for future GUI support and Microsoft Store packaging.

Repository: https://github.com/EienWolf/DisplayControl

- Lists displays and rich details (position, resolution, refresh rate, orientation, DPI scaling, HDR hints).
- Enables/disables displays and sets the primary display.
- Saves and applies display profiles (JSON) with layout (position), resolution, Hz, and orientation.
- Designed to evolve toward secure, validated, user-profile storage and a full WinUI 3 GUI.

See [ROADMAP](ROADMAP.md) for path to 1.0.0. For releases and issues, visit the GitHub repository.

## Requirements
- Windows 10/11
- .NET 8 SDK
- Console must run with sufficient privileges to change display topology.

## Project Structure
- `src/DisplayControl.Abstractions`: Public interfaces and models (IDisplayConfigurator, DisplayInfo, DesiredProfile).
- `src/DisplayControl.Windows`: Windows interop and implementation (User32/DisplayConfig, SHCore, DEVMODE).
- `src/DisplayControl.Cli`: Console application (`displayctl`).

## Build & Run
- Build the solution:
  - `dotnet build ConsoleApp.sln`
- Run the CLI:
  - `dotnet run --project src/DisplayControl.Cli -- list`

## CLI Usage
- `displayctl list [--details|-d|-v]`
  - Shows all detected displays. With `--details`, prints orientation, scaling mode, text scaling (DPI%), active vs desktop Hz, HDR hints, and color info.
- `displayctl enable <friendly>`
  - Enables a display by friendly name.
- `displayctl disable <\\.\\DISPLAYx|friendly>`
  - Disables a display by GDI name (e.g., `\\.\\DISPLAY2`) or friendly name.
- `displayctl setprimary <\\.\\DISPLAYx|friendly>`
  - Sets the primary display. Preserves relative layout by translating all displays.
- `displayctl profile <name>`
  - Applies a saved profile from `./profiles/<name>.json`. Falls back to legacy presets (`work`, `all`, `tv`) if the file is not found.
- `displayctl saveprofile [name]`
  - Saves the current state to `./profiles/<name|current>.json`.

Exit codes: `0` success, `1` error, `2` usage error.

## Profiles
Profiles can describe which monitors are enabled, which is primary, and their layout/mode settings.

Current profile JSON schema (subject to evolution until 1.0.0):

```
{
  "name": "work",
  "primaryName": "PA278CGV",
  "monitors": [
    {
      "name": "PA278CGV",
      "enabled": true,
      "positionX": 1920,
      "positionY": -627,
      "width": 2560,
      "height": 1440,
      "desiredRefreshHz": 60.0,
      "orientation": "Identity",
      "textScalePercent": 100
    },
    {
      "name": "Kamvas 22",
      "enabled": true,
      "positionX": 0,
      "positionY": 0,
      "width": 1920,
      "height": 1080,
      "desiredRefreshHz": 60.0,
      "orientation": "Rotate90",
      "textScalePercent": 100
    }
  ]
}
```

Notes:
- Primary is set first; remaining displays are positioned relative to it.
- Resolution/position/Hz/orientation are applied via a batched ChangeDisplaySettingsEx flow.
- Profiles are currently read/written under the working directory (`./profiles`). Future versions will move to `%LOCALAPPDATA%`, encrypt contents, and introduce strong validation.

## Known Limitations
- Active vs Desktop refresh rate: Active Hz uses best available signals (path refresh or DEVMODE). Dynamic refresh may not always reflect instantaneous rate.
- HDR and color details: best-effort detection; writing HDR/advanced color settings is not supported.
- Text scaling (DPI%): read-only; Windows does not expose a supported public API to set per-monitor scaling.

## Versioning
- Semantic Versioning (SemVer). The project is in 0.x while the API and profile schema evolve. 1.0.0 will ship with a stable CLI, GUI, and secure profile storage.
- See `ROADMAP.md` for milestones and Definition of Done.

## Contributing
- English-only code, names, and documentation (see [AGENTS](AGENTS.md)).
- Git Flow: feature branches off `develop`, PRs into `develop`.
- Run `dotnet format` before opening PRs.

## License
- Source code: MIT License with Commons Clause. See [LICENSE](LICENSE).
- Microsoft Store binary: governed by [EULA.txt](EULA.txt) (no redistribution, no sublicensing).

## GUI (WinUI 3)
A minimal WinUI 3 desktop app has been added under `src/DisplayControl.Gui`.

- Target: `.NET 8` with `Microsoft.WindowsAppSDK` (WinUI 3)
- Run (from solution root): open in Visual Studio 2022 (Windows) or use `dotnet build` (building requires Windows SDK + Windows App SDK tooling)
- References: `DisplayControl.Abstractions`, `DisplayControl.Windows`

This is a placeholder shell window to enable subsequent GUI work for 0.6.0.
