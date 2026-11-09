# Terraria Performance Overhaul (TPO)

A diagnostics-first performance mod for tModLoader. TPO measures first and optimizes only where
measurements justify it, while preserving vanilla and other mods' behavior.

**Status:** 0.4.0 development — diagnostics, benchmarks and benchmark scene tools. No performance optimizations yet.
Windows status: 0.1.0, 0.2.0, 0.3.0 PASSED; 0.4.0 NOT TESTED. Compatibility: `Documentation/Compatibility/CompatibilityMatrix.md`.

## Layout

| Path | Content |
|---|---|
| `TerrariaPerformanceOverhaul.cs` | `Mod` entry point (load/unload) |
| `Common/` | TPO code: build identity, configs, commands |
| `Localization/` | UI text (hjson) |
| `docs/` | Development plans 00–06 |
| `Documentation/` | Environment, test procedures, test reports |
| `Common/BenchmarkTools/` | Single-player scene setup tools (god mode, spawn, events, time, clear) |
| `Common/Diagnostics/` | Frame-time sampler, memory/GC indicators, overlay, benchmark sessions, CSV export |
| `tests/LogicTests/` | Dependency-free tests for pure logic (`scripts/test.sh`) |
| `scripts/` | Build and test helpers |
| `CLAUDE.md` | Engineering rules for AI-assisted development |

## Build (Ubuntu)

See `Documentation/Environment.md` for exact versions.

```bash
cp tml.local.props.example tml.local.props   # once, if the repo is not inside ModSources; edit the path
scripts/build.sh
```

The artifact appears in `dist/` together with its SHA-256.

## Test (Windows 10)

Follow `Documentation/Testing/WindowsSmokeTest.md`, then `Documentation/Testing/DiagnosticsTest.md`, and record the result in `Documentation/TestReports/`.

## In-game

Enable diagnostics first: Settings → Mod Configuration → TPO Client Settings.

- `/tpo` — loaded build (version, commit, tModLoader version).
- `/tpo stats` — last 1000 frames: FPS, mean/p50/p95/p99/max frame time; CPU draw/update time; process memory and GC.
- `/tpo bench <scenario> [seconds=60] [warmup=10]` — record a benchmark to CSV; `/tpo bench stop` aborts.
- Overlay: "Show overlay" in the config.
- Benchmark tools (single player, config "Enable benchmark tools"): `/tpo god`, `/tpo find`, `/tpo spawn`,
  `/tpo event`, `/tpo time`, `/tpo clear` — see `Documentation/Testing/BenchmarkTools.md`.

Benchmark files: `Documents/My Games/Terraria/tModLoader/TPO/Benchmarks/`.
Format: `Documentation/Diagnostics/CsvSchema.md`. Procedure: `Documentation/Testing/BenchmarkProtocol.md`.
