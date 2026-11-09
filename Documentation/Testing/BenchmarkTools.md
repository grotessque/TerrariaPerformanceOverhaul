# Benchmark tools (TPO 0.4.0+)

Chat commands that set up repeatable benchmark scenes without progressing in the game.

**They change the game state and, when the world is saved, the world** (time, active events, spawned/removed NPCs).
Back up the world (`.wld` and `.twld` in `Documents\My Games\Terraria\tModLoader\Worlds\`) before using them.

Requirements: Settings → Mod Configuration → TPO Client Settings → **Enable benchmark tools** (off by default);
single player; in a world. Every action is logged as `Benchmark tool: ...` in client.log.

| Command | Effect |
|---|---|
| `/tpo god [on\|off]` | Immunity to all damage (toggle without argument). Not saved; resets when you enter a world. Shown in benchmark metadata as `god_mode`. |
| `/tpo find <text>` | Lists up to 15 NPCs whose internal or display name contains the text, bosses first (gold, `[boss]`). Shows the name to use with `spawn`. |
| `/tpo spawn <npc> [count]` | Spawns like a boss summon item (`NPC.SpawnOnPlayer`: off-screen, near you). `npc` = numeric id, vanilla internal name (`KingSlime`, `EyeofCthulhu`) or `Mod/Name` for mods (find it with `/tpo find`); case does not matter. Count 1–50. |
| `/tpo event <name>` | `goblins`, `frostlegion`, `pirates`, `martians` (invasions); `bloodmoon`, `pumpkinmoon`, `frostmoon` (set night if needed); `eclipse` (sets day if needed); `stop` ends all. |
| `/tpo time <day\|noon\|night\|midnight>` | Sets the time of day. |
| `/tpo clear` | Removes hostile NPCs (not town NPCs/critters), all projectiles and items on the ground. |

Notes (verified from IL / vanilla behavior):
- Invasions (`goblins`, `frostlegion`, `pirates`, `martians`) start only if a player has consumed **at least 5 Life
  Crystals**; otherwise the command reports that it did not start. Invasions arrive from the map edge and need time.
- In Journey mode, the "enemy spawn rate" power also affects event spawns. Set it to default for event scenarios.
- Bosses keep their own despawn rules (for example, Eye of Cthulhu leaves at dawn). Freeze time (Journey power) or use
  `/tpo time` before spawning.
- With god mode the player takes no damage, so an idle player keeps the boss fight going for the whole recording.
  This is a load test, not a normal fight: no player hits, no kills.

The benchmark summary records `god_mode`, `events`, `time` and `active_bosses_start/end`, so the scene is identifiable.
