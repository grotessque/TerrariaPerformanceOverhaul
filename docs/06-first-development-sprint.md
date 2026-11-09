# Plan 06 — First development sprint: TPO foundation

**Immediate task list.** Read `CLAUDE.md` and Plan 00 first.

## Objective

Produce the first minimal TPO build that loads on the Windows test PC, reports its identity, and unloads cleanly. Do not implement speculative performance patches in this sprint.

## A. Repository reconnaissance

- [ ] Inspect repository files and generated tModLoader template.
- [ ] Detect exact tModLoader version/channel.
- [ ] Detect target framework and installed .NET SDK.
- [ ] Identify the actual build workflow.
- [ ] Build the untouched template and record output.
- [ ] Record findings in `Documentation/Environment.md`.

## B. Minimal lifecycle

- [ ] Verify main `Mod` class and internal mod name.
- [ ] Add concise load/unload logging if missing.
- [ ] Add TPO version/build identity helper.
- [ ] Ensure static state and resources are cleaned up on unload.
- [ ] Build again.

## C. Minimal configuration

- [ ] Add a master diagnostics setting only if the current tModLoader config API supports it cleanly.
- [ ] Keep diagnostics low-overhead and conservative by default.
- [ ] Do not add placeholder settings for hypothetical future optimizations.

## D. Windows smoke test

- [ ] Publish a development artifact with commit hash.
- [ ] Install it on Windows 10.
- [ ] Enable TPO and reload mods.
- [ ] Confirm startup log and mod version.
- [ ] Disable TPO and reload.
- [ ] Confirm clean unload and no new errors.
- [ ] Save test report and relevant logs.

## Sprint exit criteria

All tasks are complete, Windows test status is recorded, and a reproducible baseline exists. Only then begin Plan 01.
