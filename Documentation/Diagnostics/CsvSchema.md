# Benchmark CSV schema (schema_version 1)

`/tpo bench` writes three files per run to `<tModLoader save folder>/TPO/Benchmarks/`
(Windows: `%USERPROFILE%\Documents\My Games\Terraria\tModLoader\TPO\Benchmarks\`):

```
<yyyyMMdd-HHmmss>_<scenario>_<commit>_summary.csv
<yyyyMMdd-HHmmss>_<scenario>_<commit>_frames.csv
<yyyyMMdd-HHmmss>_<scenario>_<commit>_memory.csv
```

General rules:
- UTF-8 without BOM, `\n` line endings, comma separator, header row.
- Numbers use the invariant culture (`.` decimal separator, no thousands separator).
- An empty value means "not available / not meaningful" (for example, p99 with fewer than 100 frames).
- Files are written once, after recording ends. Nothing is written during recording.
- An aborted run (`/tpo bench stop`, leaving the world, disabling diagnostics) writes no files.

## What is measured

**Frame time** = interval between two consecutive `Main.OnPostDraw` events. In tModLoader v2026.09.3.1 this event
is raised once per drawn frame by `Main.Draw_Inner`, after `Main.DoDraw` (verified from IL). The interval includes
everything in one frame: game updates (0..n per frame), drawing, present/vsync wait and the frame limiter.
It does **not** split CPU and GPU time.

**Memory/GC values are process-wide**: they include Terraria, tModLoader and all loaded mods. They are not TPO's
own usage and are not attributed per mod.

## `_summary.csv`

Two columns: `key,value`. Rows in this order.

### Metadata (captured when the command is given, before warm-up)

| key | meaning |
|---|---|
| schema_version | this document's version |
| scenario | name given to `/tpo bench` |
| started_local, started_utc | start time |
| duration_requested_s, warmup_s | command arguments (after clamping) |
| tpo_version, tpo_commit, tpo_configuration | TPO build identity (`-dirty` = uncommitted build) |
| tml_version, tml_purpose, tml_branch | tModLoader build |
| runtime, os | .NET runtime and OS description |
| cpu_logical_cores | `Environment.ProcessorCount` |
| cpu_identifier | `PROCESSOR_IDENTIFIER` env var (Windows; family/model, not the marketing name) |
| gpu_adapter | graphics adapter description reported by FNA |
| gc_total_available_memory_bytes | memory available to the GC (approximately physical RAM or container limit) |
| gc_server | server GC mode |
| resolution, fullscreen, vsync, frame_skip_mode, lighting_mode, ui_scale, game_zoom | display/settings |
| world_name, world_size_tiles, net_mode, player_tile_position, day_time | scene |
| active_npcs_start, active_projectiles_start, active_items_start, active_dust_start, active_gore_start | entity counts at start |
| mod_count, mods | loaded mods, `Name@Version` separated by `;` (includes `ModLoader`) |
| active_*_end | entity counts at the end of recording |

### Results (computed from the recorded frames)

| key | unit | meaning |
|---|---|---|
| duration_recorded_s | s | actual recording length |
| truncated | bool | recording stopped early because the frame buffer (1000 frames/s × duration) was full |
| frames | count | recorded frames |
| fps_avg | FPS | 1000 / frame_ms_mean |
| frame_ms_mean, frame_ms_min, frame_ms_max | ms | |
| frame_ms_p50, frame_ms_p95, frame_ms_p99 | ms | nearest-rank percentiles; p95 needs ≥ 20 frames, p99 ≥ 100 |
| frames_over_16_7ms, frames_over_33_3ms | count | frames slower than 60 / 30 FPS |
| updates, updates_per_s | count, 1/s | `ModSystem.PostUpdateEverything` calls (in-world game updates) |
| unfocused_frames, paused_frames | count | frames with the window unfocused / game paused; > 0 means the run is suspect |
| alloc_bytes, alloc_bytes_per_s | B, B/s | process managed allocations during recording |
| gen0/gen1/gen2_collections | count | GC collections during recording |
| gc_pause_ms | ms | GC pause time during recording |
| managed_heap_bytes_start/end | B | `GC.GetTotalMemory(false)` |
| working_set_bytes_start/end/max | B | `Environment.WorkingSet` (sampled at about 1 Hz) |
| tpo_diagnostics_ms_total, tpo_diagnostics_us_per_frame | ms, µs | time spent in TPO diagnostics code while recording (self-measured; see BenchmarkProtocol.md for external validation) |

## `_frames.csv`

| column | unit | meaning |
|---|---|---|
| frame_index | | 0-based |
| t_end_ms | ms | end of this frame, relative to recording start |
| frame_ms | ms | frame time |

## `_memory.csv`

One row at recording start, then about every second, then one at the end.

| column | unit | meaning |
|---|---|---|
| t_s | s | relative to recording start |
| managed_heap_bytes | B | `GC.GetTotalMemory(false)` |
| allocated_bytes_total | B | `GC.GetTotalAllocatedBytes(false)`, cumulative since process start |
| gen0_collections, gen1_collections, gen2_collections | count | cumulative `GC.CollectionCount(n)` |
| gc_pause_ms_total | ms | cumulative `GC.GetTotalPauseDuration()` |
| working_set_bytes | B | `Environment.WorkingSet` |
