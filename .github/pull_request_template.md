## Summary
Briefly describe what this PR does and why.

## Related Issues
Closes #123
Refs #456

## Scope
Select all that apply:
- [ ] [cli]
- [ ] [windows]
- [ ] [abstractions]
- [ ] [docs]

## Details
What changed, key decisions, and any notable implementation notes.

## How to Test
Steps to validate locally:
1) `dotnet restore`
2) `dotnet build`
3) Run: `dotnet run --project src/DisplayControl.Cli -- list`
4) (If applicable) Additional commands / scenarios to verify

## Sample CLI Output (if applicable)
```text
$ dotnet run --project src/DisplayControl.Cli -- list
# Paste trimmed, relevant output here
```

## Screenshots (optional)
Attach images/GIFs for UX-visible changes.

## Breaking Changes
- [ ] Yes (describe migration/impact below)
- [ ] No

If yes, explain the break and any migration steps.

## Checklist
- [ ] Targets correct base branch (develop for features; master only for release/* or hotfix/*)
- [ ] English-only names, strings, and documentation
- [ ] `dotnet build` succeeds
- [ ] `dotnet format` produces no changes
- [ ] `dotnet test` passes (if tests exist/affected)
- [ ] Docs updated as needed ([README](../README.md), [ROADMAP](../ROADMAP.md), [AGENTS](../AGENTS.md))
- [ ] For Windows interop: added XML summaries + link to Microsoft Docs
- [ ] CLI help/output updated if behavior or options changed
- [ ] No commented-out code; minimal, meaningful inline comments only
- [ ] Considered permissions/safety (display reconfiguration can blank screens)
