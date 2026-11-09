# Compatibility matrix

A combination is listed as PASSED only after a recorded test with the exact versions below.
Versions come from the `mods` field of benchmark summaries or from client.log.

Status values: `PASSED`, `FAILED`, `NOT TESTED`.

| TPO | tModLoader | Mod combination (exact versions) | Load/unload | Benchmarks | Report |
|---|---|---|---|---|---|
| 0.3.0 (1321afee3c9b) | 2026.9.3.1 | TPO only | PASSED | PASSED | TestReports/2026-11-09-0.3.0-1321afee3c9b.md |
| 0.3.0 (1321afee3c9b) | 2026.9.3.1 | CalamityMod 2.2.4 + CalamityModMusic 2.1 | PASSED | PASSED (idle scene) | TestReports/2026-11-09-0.3.0-calamity.md |
| — | — | Fargo's Souls | NOT TESTED | NOT TESTED | — |
| — | — | Calamity + Infernum | NOT TESTED | NOT TESTED | — |
| — | — | Thorium | NOT TESTED | NOT TESTED | — |

TPO has no integration code for any of these mods. "Compatible" here only means: loads, unloads and measures
correctly together with that mod, with no errors attributable to TPO.
