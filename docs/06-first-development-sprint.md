# Plan 06 — First development sprint: TPO foundation

**Immediate task list.** Read `CLAUDE.md` and Plan 00 first.

## Objective

Produce the first minimal TPO build that loads on the Windows test PC, reports its identity, and unloads cleanly. Do not implement speculative performance patches in this sprint.

## A. Repository reconnaissance

- [x] Inspect repository files and generated tModLoader template.
- [x] Detect exact tModLoader version/channel.
- [x] Detect target framework and installed .NET SDK.
- [x] Identify the actual build workflow.
- [x] Build the untouched template and record output.
- [x] Record findings in `Documentation/Environment.md`.

## B. Minimal lifecycle

- [x] Verify main `Mod` class and internal mod name.
- [x] Add concise load/unload logging if missing.
- [x] Add TPO version/build identity helper.
- [x] Ensure static state and resources are cleaned up on unload.
- [x] Build again.

## C. Minimal configuration

- [x] Add a master diagnostics setting only if the current tModLoader config API supports it cleanly.
- [x] Keep diagnostics low-overhead and conservative by default.
- [x] Do not add placeholder settings for hypothetical future optimizations.

## D. Windows smoke test

- [x] Publish a development artifact with commit hash.
- [x] Install it on Windows 10.
- [x] Enable TPO and reload mods.
- [x] Confirm startup log and mod version.
- [x] Disable TPO and reload.
- [x] Confirm clean unload and no new errors.
- [x] Save test report and relevant logs.

## Sprint exit criteria

All tasks are complete, Windows test status is recorded, and a reproducible baseline exists. Only then begin Plan 01.
