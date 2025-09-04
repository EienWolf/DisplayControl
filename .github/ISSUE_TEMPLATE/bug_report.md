---
name: "Bug report"
about: "Report a problem with DisplayControl (CLI or Windows services)"
title: "[Bug]: "
labels: [bug]
assignees: []
---

## Description
A clear, concise description of the bug.

## Reproduction Steps
- Command(s) run (include full output):
  - Example: `dotnet run --project src/DisplayControl.Cli -- list`
- What you expected to happen
- What actually happened

## Display Setup
- Number of monitors and arrangement (primary, extended, mirroring)
- Display identifiers (e.g., `\\.\DISPLAY1`, `\\.\DISPLAY2`) and friendly names from `displayctl list`
- GPU/adapter(s) and connection types (HDMI/DP/USB-C)
- Any profiles used (e.g., `work`, `tv`, `all`)

## Environment
- OS: Windows 10/11 (build/version)
- .NET SDK: output of `dotnet --info`
- Architecture: x64
- Terminal: Windows Terminal/PowerShell/CMD
- Project commit/ref (if building locally):

## Logs / Screenshots
- Relevant console output
- Exceptions/stack traces if available

## Additional Context
- Were you screen sharing or on battery power?
- Recent changes before the issue appeared?

---
Safety note: Display reconfiguration can briefly blank screens. Avoid running during critical sessions or screen sharing.

