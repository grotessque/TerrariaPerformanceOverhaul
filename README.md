# Terraria Performance Overhaul (TPO)

A diagnostics-first performance mod for tModLoader. TPO measures first and optimizes only where
measurements justify it, while preserving vanilla and other mods' behavior.

**Status:** 0.1.0 development. Foundation only: build identity, configuration, clean load/unload.
No performance optimizations yet. Windows status: NOT TESTED.

## Layout

| Path | Content |
|---|---|
| `TerrariaPerformanceOverhaul.cs` | `Mod` entry point (load/unload) |
| `Common/` | TPO code: build identity, configs, commands |
| `Localization/` | UI text (hjson) |
| `docs/` | Development plans 00–06 |
| `Documentation/` | Environment, test procedures, test reports |
| `scripts/` | Build helpers |
| `CLAUDE.md` | Engineering rules for AI-assisted development |

## Build (Ubuntu)

See `Documentation/Environment.md` for exact versions.

```bash
cp tml.local.props.example tml.local.props   # once, if the repo is not inside ModSources; edit the path
scripts/build.sh
```

The artifact appears in `dist/` together with its SHA-256.

## Test (Windows 10)

Follow `Documentation/Testing/WindowsSmokeTest.md` and record the result in `Documentation/TestReports/`.

## In-game

- `/tpo` — show the loaded build (version, commit, tModLoader version).
- Settings → Mod Configuration → TPO Client Settings.
