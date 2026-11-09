# Build and test environment

Recorded: 2026-11-09. Update this file whenever a version below changes.

## Ubuntu (development / build computer)

| Item | Value | How detected |
|---|---|---|
| OS | Ubuntu, Linux kernel 7.0.0-42-generic | `uname -r` |
| .NET SDK | 8.0.133 (`/usr/lib/dotnet/sdk`) | `dotnet --list-sdks` |
| .NET runtime | Microsoft.NETCore.App 8.0.33 | `dotnet --list-runtimes` |
| tModLoader | **v2026.09.3.1** (stable channel, `pre=False` on GitHub) | GitHub Releases API |
| tModLoader package | `tModLoader.zip` from the GitHub release, SHA-256 `5aaed81b245044286c3ade200f8bac9162ef932ee8ff11a31e05da49a186c07b` | `sha256sum` |
| tModLoader install path | `~/tools/tModLoader-v2026.09.3.1/` (not a Steam install) | — |
| Target framework | `net8.0`, C# `LangVersion 12.0` | `tMLMod.targets` of that install |
| Define constant | `TML_2026_09` | `tMLMod.targets` |

No Steam/tModLoader game install is present on Ubuntu; the GitHub release package is used only for building.

## Build workflow

The real tModLoader workflow is: `dotnet build` → `tMLMod.targets` → target `BuildMod` runs
`dotnet tModLoader.dll -server -build <ProjectDir> -eac <compiled dll> ...`, which packages the `.tmod`
into `~/.local/share/Terraria/tModLoader/Mods/TerrariaPerformanceOverhaul.tmod`.

The repository lives outside ModSources (`~/projects/TerrariaPerformanceOverhaul`), so the `.csproj`
locates `tMLMod.targets` in this order:

1. `-p:TmlTargetsPath=...` (command line or environment variable `TmlTargetsPath`);
2. `tml.local.props` (machine-specific, Git-ignored; copy from `tml.local.props.example`);
3. `..\tModLoader.targets` (standard ModSources layout, unchanged template behavior).

Build command:

```bash
scripts/build.sh            # Release; writes dist/TerrariaPerformanceOverhaul-<version>-<commit>.tmod + .sha256
```

### Build identity

`TerrariaPerformanceOverhaul.csproj` injects `git rev-parse --short=12 HEAD` (plus `-dirty` for uncommitted
changes) as `SourceRevisionId`; the .NET SDK appends it to `AssemblyInformationalVersion`.
`Common/TPOBuildIdentity.cs` reads it at runtime. Without Git, the commit is reported as `unknown`.

## Windows 10 (runtime test computer)

| Item | Value |
|---|---|
| Windows build | TODO: record (`winver`) |
| tModLoader version / channel | v2026.9.3.1 (= v2026.09.3.1), stable — reported by the developer 2026-11-09; matches the build version |
| Hardware (CPU / GPU / RAM) | TODO: record |
| Mods folder | `%USERPROFILE%\Documents\My Games\Terraria\tModLoader\Mods\` |
| Logs | `%ProgramFiles(x86)%\Steam\steamapps\common\tModLoader\tModLoader-Logs\client.log` (Steam install) |

## Baseline build results

| Date | Commit | Result | Notes |
|---|---|---|---|
| 2026-11-09 | 4f5a667 (untouched template) | NOT RUN to completion | First attempt from the real path failed with `NETSDK1013` because `..\tModLoader.targets` does not exist outside ModSources. Fixed by the `TmlTargetsPath` resolution above. |
| 2026-11-09 | f83727af71ec | PASSED (build: 0 warnings, 0 errors) | `scripts/build.sh`; Windows smoke test PASSED, see `TestReports/2026-11-09-0.1.0-f83727af71ec.md`. |
