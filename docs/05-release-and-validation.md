# Plan 05 — Release, regression testing, and maintenance

**Depends on:** Plans 00–04 as applicable. **Goal:** Produce reproducible builds and maintain a trustworthy compatibility/performance record.

## Build identity

Every build should identify TPO version, source commit, tModLoader version/channel, target framework/toolchain, build date, and development/release status. Never publish a build if compilation failed or its source commit is unknown.

## Automation

After local builds are reliable, configure GitHub Actions to check out an exact commit, install the documented SDK, obtain the pinned tModLoader build dependencies, build using the verified workflow, and upload `.tmod` plus logs only on success. Do not assume generic `dotnet build` is equivalent to tModLoader's build process; validate CI against the generated project.

## Windows validation checklist

- Install the exact release artifact and verify its hash when provided.
- Verify TPO loads and unloads.
- Check logs for errors and warnings.
- Run agreed benchmark scenarios.
- Test declared mod combinations.
- Check world save/load and extended gameplay.
- Record crashes, regressions, and known issues.

## Versioning

Use versions such as `0.x.y-dev.n` for development builds and `0.x.y` for tested releases. Compilation alone does not make a build stable.

## Acceptance criteria

Every artifact maps to a known source commit. Build/test environments are documented. Windows status is explicit. Release notes list changes, known issues, tested mod versions, and benchmark context. Keep previous known-good artifacts for rollback.
