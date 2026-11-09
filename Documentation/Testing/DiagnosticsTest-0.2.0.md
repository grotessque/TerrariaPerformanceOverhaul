# Diagnostics functional test (0.2.0)

Run after the smoke test (`WindowsSmokeTest.md`, version 0.2.0) passes.

1. Config: "Enable diagnostics" and "Show overlay" are off by default. Type `/tpo stats`.
   Expected: message to enable diagnostics first.
2. Enable "Enable diagnostics" only. Log line: `Config: EnableDiagnostics = True, ShowOverlay = False`.
   No overlay is visible.
3. `/tpo stats`. Expected: 2 lines; frame count up to 1000. If an independent FPS counter is available
   (Steam overlay FPS counter, PresentMon, CapFrameX), the average FPS should be close to it.
   With the default frame limit, FPS should be close to 60 and mean frame time close to 16.7 ms.
4. Enable "Show overlay". Expected: 3 lines bottom-right, updated twice per second. Values change when you
   move around a busy area.
5. `/tpo bench test 10 2`. Expected: start message; after about 12 s, "finished" message and the folder name.
   The overlay shows a 4th line with the bench state while it runs.
6. Open `Documents\My Games\Terraria\tModLoader\TPO\Benchmarks\`. Expected: three CSV files.
   `_summary.csv` contains `tpo_commit` matching the build and `frames` > 0. `_frames.csv` has `frames` + 1 lines.
7. `/tpo bench test 30 0`, then `/tpo bench stop` after a few seconds. Expected: "aborted" message, no new files.
8. Start `/tpo bench test 30 0`, then exit the world to the menu. Expected: log line `aborted: the world was unloaded`,
   no new files, no errors.
9. Error handling: `/tpo bench`, `/tpo bench a b`, `/tpo bench bad.name`, `/tpo bench test 9999`.
   Expected: usage/validation messages, nothing starts.
10. Disable "Enable diagnostics" while a bench runs. Expected: "aborted: diagnostics were disabled", overlay disappears.
11. Disable TPO, reload mods, enable again, reload. Expected: no errors in `client.log`.

Then run the benchmark protocol (`BenchmarkProtocol.md`) for `spawn-idle` and `spawn-idle-overlay`, 3 runs each,
and the external overhead check (section 4) if possible.
