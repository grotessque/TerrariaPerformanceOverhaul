# Plan 01 — Diagnostics and reproducible baselines

**Depends on:** Plan 00. **Goal:** Make performance measurable before attempting to improve it.

## Tasks

1. Add a diagnostics configuration section with safe defaults and a master enable/disable switch.
2. Implement a low-overhead rolling frame-time sampler using APIs verified for the pinned tModLoader version.
3. Report FPS and frame-time distributions; include p50/p95/p99 when sample counts support them.
4. Collect available process-memory, managed-allocation, and GC metrics without forcing collections.
5. Add timing scopes only for TPO-owned modules; do not claim per-mod attribution without real attribution.
6. Add a compact in-game display only if supported APIs allow it without meaningful overhead.
7. Export CSV with documented columns and invariant numeric formatting.
8. Record metadata: TPO version/commit, tModLoader version, OS, hardware, enabled mods, resolution, settings, scenario, duration.
9. Define repeatable Windows test scenarios and warm-up procedure.
10. Measure diagnostics overhead by comparing enabled and disabled runs.

## Benchmark protocol

- Keep world, character, location, camera, resolution, settings, and mod list constant.
- Warm up before recording; run multiple samples of equal duration.
- Record raw CSV, not just averages.
- Separate loading tests from steady-state gameplay.
- Document background activity and test variability.

## Acceptance criteria

- Diagnostics can be disabled.
- No per-frame disk writes or excessive logging.
- Exports identify the exact build and test scenario.
- Baselines are repeatable enough to compare, with variability documented.
- Diagnostics overhead is measured.
- No performance-improvement claims are made yet.
