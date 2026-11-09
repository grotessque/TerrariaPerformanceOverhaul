# Plan 00 — Project baseline and first loadable build

**Goal:** Establish a clean, reproducible TPO baseline before performance work.

## Tasks

1. Inspect the repository, generated tModLoader template, project files, and current Git state.
2. Detect exact tModLoader version/channel, target framework, .NET SDK, and supported build workflow.
3. Build the untouched template and record the baseline output.
4. Verify mod internal name, display name, author, and version metadata.
5. Verify the minimal `Mod` entry point and add concise load/unload logging if needed.
6. Add a small version/build identity helper. Include commit metadata only if it can be injected reliably; do not invent it.
7. Ensure static state, hooks, events, and resources are cleaned up on unload.
8. Document the environment in `Documentation/Environment.md`.
9. Build the `.tmod` on Ubuntu using the real tModLoader workflow.
10. Transfer it to Windows and verify enable, load, disable, and unload.

## Acceptance criteria

- Project builds without errors in the recorded environment.
- TPO appears in the mod list and loads/unloads cleanly.
- Logs identify TPO and its version without excessive output.
- No speculative hooks, patches, caches, or worker threads exist.
- Windows smoke-test result is recorded.

## Out of scope

FPS patches, memory pooling, IL edits, multithreading, and mod-specific compatibility patches.
