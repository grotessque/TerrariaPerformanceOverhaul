# Plan 03 — Compatibility framework and mod adapters

**Depends on:** Plans 00–02. **Goal:** Coexist safely with large content mods without hard dependencies or brittle assumptions.

## Target ecosystem

- Calamity Mod.
- Fargo's Souls Mod and relevant Fargo components.
- Infernum Mode, tested with its actual dependency set (including Calamity where required).
- Thorium Mod.
- Additional large modpacks chosen from real user test reports.

## Tasks

1. Implement optional-mod detection using supported tModLoader APIs.
2. Record detected mod names and versions in diagnostics.
3. Define a small adapter lifecycle only if needed: detect, validate, initialize, disable, unload.
4. Keep adapters isolated from TPO core.
5. Track supported, untested, and unsupported versions explicitly.
6. Ensure adapter failure disables only that adapter when safe.
7. Avoid direct optional-mod assembly references in core unless the packaging model explicitly supports them.
8. Avoid private reflection and IL edits unless separately justified.
9. Maintain a compatibility matrix with exact tested versions.

## Adapter rules

Verify the exact mod version and public API before integration. Never assume private implementation details are stable. Validate signatures/capabilities. If an expected API is absent, skip the adapter and explain why in the log. Optional mod absence or incompatibility must not prevent TPO from loading.

## Minimum test matrix

- TPO alone.
- TPO + Calamity.
- TPO + Fargo.
- TPO + Calamity + Infernum.
- TPO + Thorium.
- A representative large modpack.

Test startup, world load, relevant boss/event scenarios, unload/reload, and logs.

## Acceptance criteria

Core TPO loads without optional mods. Adapter status and exact versions are reported. Unsupported combinations fail safely. Compatibility claims match recorded tests.
