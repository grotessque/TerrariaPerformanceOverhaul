# Plan 04 — Carefully constrained background processing

**Depends on:** Plans 00–03. **Goal:** Use background work only when profiling proves it useful and data ownership is safe.

## Entry gate

Do not start this phase without a measured CPU-bound workload that can be separated from live gameplay state. Multithreading can make performance worse through copying, synchronization, scheduling, and contention.

## Allowed design

1. Main thread captures a minimal immutable snapshot.
2. Worker processes only copied/immutable data proven safe to access.
3. Worker returns a result without mutating live game state.
4. Main thread validates result freshness and applies it at a safe point.

## Required safeguards

- Bounded worker count and queue capacity.
- Explicit overload policy.
- Cancellation and reliable shutdown.
- Exception reporting.
- Stale-result rejection.
- Main-thread result handoff.
- Metrics for queue depth, wait time, execution time, and cancelled/dropped work.

## Prohibited

Do not access/mutate live Terraria entities or world state from workers without an explicit thread-safety guarantee. Never call graphics/rendering APIs from workers. Do not create unbounded tasks per tick/frame or parallelize AI, collision, world generation, or simulation updates without a proven deterministic design and explicit approval. All workers must stop on mod unload.

## Acceptance criteria

A documented workload shows repeatable benefit; gameplay behavior remains correct; queue/workers are bounded; unload, overload, cancellation, and exceptions are tested; feature is independently configurable and disabled by default until validated.
