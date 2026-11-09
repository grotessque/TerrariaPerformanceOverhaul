# Windows smoke test (Plan 00 / Sprint 06 D)

Repeatable manual procedure for every build that goes to the Windows 10 test computer.
Save the result as `Documentation/TestReports/<date>-<version>-<commit>.md` (use `TEMPLATE.md`).

## Preconditions

- The artifact was built from a **committed** state (file name must not contain `-dirty`).
- You have the `.tmod` and its `.sha256` from `dist/` (or the GitHub Release).
- tModLoader on Windows is the version recorded in `Documentation/Environment.md`.

## Steps

1. Close tModLoader.
2. Verify the hash in PowerShell:
   `Get-FileHash .\TerrariaPerformanceOverhaul-<version>-<commit>.tmod -Algorithm SHA256`
   and compare with the `.sha256` file.
3. Copy the file to `%USERPROFILE%\Documents\My Games\Terraria\tModLoader\Mods\` and **rename it to
   `TerrariaPerformanceOverhaul.tmod`** (tModLoader requires the internal mod name as the file name).
   Remove any older `TerrariaPerformanceOverhaul.tmod` first.
   If tModLoader reports that the file "was downloaded from the internet … has not been trusted", Windows has marked
   the copied file (Mark-of-the-Web). Either trust it in tModLoader or, before starting the game, run
   `Unblock-File .\TerrariaPerformanceOverhaul.tmod` in PowerShell (or File Properties → Unblock).
4. Start tModLoader. Open **Workshop → Manage Mods**. Confirm "TPO" is listed with version of the build under test (for example `0.2.0`).
5. Enable TPO, click **Reload Mods**. Expected: no error screen.
6. Open `client.log`. Expected exactly one line like:
   `[TerrariaPerformanceOverhaul]: Loading. TPO <version> (commit <commit>, Release) | tModLoader <version> ...`
   The commit must match the artifact file name.
7. Open **Settings → Mod Configuration → TPO → TPO Client Settings**. Confirm "Enable diagnostics" exists,
   default off. Toggle it on, save. Expected log line: `Config: EnableDiagnostics = True`. Toggle back off.
8. Create/enter a world. Type `/tpo` in chat. Expected: 4 lines with version, commit, tModLoader version,
   runtime/OS and diagnostics state.
9. Exit to main menu. Disable TPO, **Reload Mods**. Expected log line: `[TerrariaPerformanceOverhaul]: Unloading.`
   and no new errors/warnings mentioning TerrariaPerformanceOverhaul.
10. Enable TPO again and reload once more (re-load after unload). Expected: same as step 6, no errors.
11. Save `client.log` next to the test report.

## Result

`PASSED` only if all steps match the expectations. Otherwise `FAILED`, with the step number and the log excerpt.
