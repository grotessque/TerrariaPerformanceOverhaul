# Benchmark protocol (Plan 01)

Purpose: produce baselines that can be compared between builds. A number without its scenario,
build and variability is not a result.

## 1. Fixed test setup (create once, reuse for every run)

| Item | Value |
|---|---|
| World | Small, seed `tpobench1`, Journey mode, Corruption. Name: `TPO Bench 1`. Keep a backup copy of the `.wld` file. |
| Character | Journey mode character `TPO Bench`. No accessories that create particles or light. |
| Journey powers | Time: frozen at noon. Weather: frozen (no rain/wind). Enemy spawn rate: 0. |
| Position | World spawn point. Do not move the camera or the character during recording. |
| Display | Record the values; keep them identical across compared runs (resolution, fullscreen, vsync, frame skip, lighting mode, zoom). |
| Mod list | Exactly the list for the scenario. `mods` in the summary must be identical across compared runs. |
| Machine | Power plan "High performance", charger connected (laptops). Close browsers, launchers, overlays and recorders. |

## 2. Scenarios

| Scenario name | Mods | Settings | What it shows |
|---|---|---|---|
| `spawn-idle` | TPO only | Diagnostics on, overlay off | Steady-state baseline, vanilla content |
| `spawn-idle-overlay` | TPO only | Diagnostics on, overlay on | Overlay cost vs `spawn-idle` |
| `spawn-idle-<modpack>` | TPO + modpack (e.g. `calamity`) | Diagnostics on, overlay off | Baseline for a mod combination (Plan 03) |

Add new scenarios here before using them. Use only letters, digits, `_` and `-` in names.

## 2a. Which metrics to compare

With vsync on (the default), `frame_ms_*` is pinned to the refresh period (60 Hz → 16.7 ms) and shows pacing, not
work. Measured in 0.2.0: mean frame time equalled the refresh period in every run. Therefore:

- **Primary (vsync-independent):** `draw_cpu_ms_mean/p95`, `update_ms_per_update_mean`, `update_ms_per_frame_p95`,
  per-category `update_*_ms_per_update`, `alloc_bytes_per_s`, `gen0_collections`, `gc_pause_ms`.
- **Hitches:** `frame_ms_p99`, `frame_ms_max`, `frames_over_2x_p50`, `frames_over_33_3ms`.
- **Throughput (optional):** an extra run with vsync off. Record it as a separate scenario (suffix `-novsync`);
  check `updates_per_s` — if it is not about 60, game speed changed and the run is not comparable with vsync runs.

## 3. Procedure for one measurement set

1. Start tModLoader with the required mod list. Enable TPO diagnostics in the config.
2. Load the bench world. Wait **30 s** after the world appears (asset loading, initial lighting).
3. Do not touch the mouse or keyboard after giving the command. Keep the window focused.
4. Run: `/tpo bench spawn-idle 60 10` (10 s warm-up, 60 s recording).
5. Wait for the "finished" message. Repeat steps 4–5 for **3 runs** in total.
6. Check every summary: `unfocused_frames = 0`, `paused_frames = 0`, `truncated = False`.
   A run that fails a check is discarded and repeated.
7. Report all runs, not only the best. Variability: report min–max of `fps_avg` and `frame_ms_p95` across the runs.
   If the spread of `frame_ms_p95` is larger than about 5 %, the setup is not stable enough to compare small changes;
   find the cause (background activity, thermals, autosave) before drawing conclusions.

Loading time is not measured by these scenarios; do not compare it from these files.

## 4. Diagnostics overhead (acceptance criterion of Plan 01)

TPO cannot measure frames while diagnostics are off, so overhead is measured two ways:

**a) Self-measured (every run):** `tpo_diagnostics_us_per_frame` in the summary — time spent inside TPO
diagnostics code per frame. Compare it with `frame_ms_mean` (16.7 ms at 60 FPS = 16 700 µs).

**b) External (once per diagnostics change):** an independent frame-time tool on Windows, for example
[PresentMon](https://github.com/GameTechDev/PresentMon) or [CapFrameX](https://github.com/CXWorld/CapFrameX).
Same world/position, 3 × 60 s captures for each configuration:

| Config | TPO in mod list | Diagnostics | Overlay |
|---|---|---|---|
| A | disabled | — | — |
| B | enabled | off | off |
| C | enabled | on | off |
| D | enabled | on | on |

Compare A–B (cost of TPO being loaded), B–C (cost of measurement), C–D (cost of overlay) using average
frame time and p95/p99. Differences smaller than the run-to-run spread are reported as "not measurable",
not as zero and not as a gain. For a stable result, set the frame limit to "Off" with vsync off.

## 5. Storing results

Copy the three CSV files of every run into a folder named
`<date>_<tpo version>_<commit>_<scenario>` and attach it to the test report
(`Documentation/TestReports/`). Large raw files can be stored outside Git; the report must name where they are.
