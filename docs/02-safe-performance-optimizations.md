# Plan 02 — Safe, measured performance optimizations

**Depends on:** Plans 00 and 01. **Goal:** Implement only optimizations supported by profiling evidence.

## Candidate areas

- Avoidable allocations in TPO-owned code.
- Repeated calculations with safe, explicit cache invalidation.
- Redundant work in TPO-owned update paths.
- Diagnostics overhead.
- Data structures owned by TPO.

Do not choose targets by intuition. If profiling reveals no actionable bottleneck, improve measurement rather than adding speculative code. Initially avoid patching Terraria or other mods' hot methods.

## Workflow for every optimization

1. Create an isolated feature branch.
2. Capture a baseline for the target scenario.
3. Identify the exact hot path and explain why the change preserves behavior.
4. Implement one change behind a configuration switch when risk is non-trivial.
5. Build and run correctness tests.
6. Test TPO alone and relevant mod combinations.
7. Repeat the same benchmark after the change.
8. Compare frame-time distributions, allocations/GC, and memory indicators.
9. Keep the change only if results are repeatable and behavior remains correct.
10. Document measurements, limitations, and rollback control.

## Safety rules

Preserve update order, collision, AI, RNG, networking, and callbacks. No unbounded caches, forced GC, speculative object pooling, broad IL patching, or hidden gameplay changes.

## Acceptance criteria

Every optimization has before/after measurements, correctness validation, an appropriate disable/rollback path, and unload cleanup where needed.
