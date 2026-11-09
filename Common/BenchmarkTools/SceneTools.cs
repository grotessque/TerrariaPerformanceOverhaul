using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaPerformanceOverhaul.Common.Configs;

namespace TerrariaPerformanceOverhaul.Common.BenchmarkTools
{
	/// <summary>
	/// Single-player tools that set up benchmark scenes: spawn NPCs/bosses, start events, set time, clear the scene,
	/// god mode. They change the game state (and the world when it is saved), so they are disabled by default and
	/// only work in single player. Main thread only (called from chat commands).
	/// </summary>
	public static class SceneTools
	{
		public const int MaxSpawnCount = 50;

		/// <summary>Returns null when the tools may be used, otherwise the reason.</summary>
		public static string CheckAvailable() {
			if (!(ModContent.GetInstance<TPOClientConfig>()?.EnableBenchmarkTools ?? false))
				return "benchmark tools are disabled: Settings > Mod Configuration > TPO Client Settings > Enable benchmark tools";
			if (Main.netMode != NetmodeID.SinglePlayer)
				return "benchmark tools work only in single player";
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active)
				return "enter a world first";
			return null;
		}

		// ---- NPC lookup ----

		/// <summary>
		/// Resolves a numeric id, a vanilla internal name (e.g. "KingSlime") or "Mod/Name" (e.g. "CalamityMod/...").
		/// Exact match first; then a case-insensitive match on the internal name.
		/// </summary>
		public static bool TryResolveNpc(string name, out int type) {
			type = 0;
			if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) {
				if (id <= 0 || id >= NPCLoader.NPCCount)
					return false;
				type = id;
				return true;
			}

			if (name.Contains('/') && ModContent.TryFind(name, out ModNPC modNpc)) {
				type = modNpc.Type;
				return true;
			}

			if (!name.Contains('/') && NPCID.Search.TryGetId(name, out type) && type > 0)
				return true;

			for (int t = 1; t < NPCLoader.NPCCount; t++) {
				if (string.Equals(InternalName(t), name, StringComparison.OrdinalIgnoreCase)) {
					type = t;
					return true;
				}
			}

			type = 0;
			return false;
		}

		public static string InternalName(int type) =>
			type < NPCID.Count ? NPCID.Search.GetName(type) : NPCLoader.GetNPC(type)?.FullName ?? type.ToString(CultureInfo.InvariantCulture);

		public static bool IsBoss(int type) =>
			ContentSamples.NpcsByNetId.TryGetValue(type, out NPC sample) && sample.boss;

		/// <summary>NPC types whose internal or display name contains <paramref name="filter"/>; bosses first.</summary>
		public static List<(int Type, string Internal, string Display, bool Boss)> Find(string filter, int limit) {
			var result = new List<(int, string, string, bool)>();
			for (int type = 1; type < NPCLoader.NPCCount; type++) {
				string internalName = InternalName(type);
				string display = Lang.GetNPCNameValue(type);
				if (internalName.Contains(filter, StringComparison.OrdinalIgnoreCase) || display.Contains(filter, StringComparison.OrdinalIgnoreCase))
					result.Add((type, internalName, display, IsBoss(type)));
			}
			return result.OrderByDescending(r => r.Item4).ThenBy(r => r.Item1).Take(limit).ToList();
		}

		// ---- Actions ----

		/// <summary>Spawns <paramref name="count"/> NPCs the way boss summon items do (off-screen, near the player).</summary>
		public static void Spawn(int type, int count) {
			for (int i = 0; i < count; i++)
				NPC.SpawnOnPlayer(Main.myPlayer, type);
		}

		public static string SetTime(string when) {
			switch (when) {
				case "day":
					Main.dayTime = true;
					Main.time = 0;
					return "time set to dawn (day)";
				case "noon":
					Main.dayTime = true;
					Main.time = 27000;
					return "time set to noon";
				case "night":
					Main.dayTime = false;
					Main.time = 0;
					return "time set to dusk (night)";
				case "midnight":
					Main.dayTime = false;
					Main.time = 16200;
					return "time set to midnight";
				default:
					return null;
			}
		}

		public const string EventNames = "goblins, frostlegion, pirates, martians, bloodmoon, eclipse, pumpkinmoon, frostmoon, stop";

		/// <summary>Starts or stops an event. Returns a message for the player; null for an unknown event.</summary>
		public static string StartEvent(string name) {
			switch (name) {
				case "goblins":
					return StartInvasion(InvasionID.GoblinArmy, "Goblin Army");
				case "frostlegion":
					return StartInvasion(InvasionID.SnowLegion, "Frost Legion");
				case "pirates":
					return StartInvasion(InvasionID.PirateInvasion, "Pirate Invasion");
				case "martians":
					return StartInvasion(InvasionID.MartianMadness, "Martian Madness");
				case "bloodmoon":
					string bm = EnsureNight();
					Main.bloodMoon = true;
					return "Blood Moon started" + bm;
				case "eclipse":
					string ec = EnsureDay();
					Main.eclipse = true;
					return "Solar Eclipse started" + ec;
				case "pumpkinmoon":
					string pm = EnsureNight();
					Main.startPumpkinMoon();
					return "Pumpkin Moon started" + pm;
				case "frostmoon":
					string fm = EnsureNight();
					Main.startSnowMoon();
					return "Frost Moon started" + fm;
				case "stop":
					Main.invasionType = 0;
					Main.invasionSize = 0;
					Main.bloodMoon = false;
					Main.eclipse = false;
					Main.pumpkinMoon = false;
					Main.snowMoon = false;
					return "all events stopped (already spawned enemies remain; use /tpo clear)";
				default:
					return null;
			}
		}

		private static string StartInvasion(int invasionType, string label) {
			if (Main.invasionType != 0)
				return $"{label} not started: another invasion is active (use /tpo event stop)";

			Main.StartInvasion(invasionType);
			if (Main.invasionType != invasionType)
				// Verified from IL: StartInvasion does nothing unless a player has consumed at least 5 Life Crystals.
				return $"{label} not started: the game requires a player with at least 5 Life Crystals consumed";

			return $"{label} started; it arrives from the edge of the map and needs some time to reach you";
		}

		private static string EnsureNight() {
			if (!Main.dayTime)
				return "";
			SetTime("night");
			return " (time set to night)";
		}

		private static string EnsureDay() {
			if (Main.dayTime)
				return "";
			SetTime("day");
			return " (time set to day)";
		}

		/// <summary>Removes hostile NPCs, all projectiles and items on the ground. Town NPCs and critters stay.</summary>
		public static (int Npcs, int Projectiles, int Items) Clear() {
			int npcs = 0, projectiles = 0, items = 0;
			foreach (NPC npc in Main.npc) {
				if (npc != null && npc.active && !npc.townNPC && !npc.friendly && npc.lifeMax > 5) {
					npc.active = false;
					npcs++;
				}
			}
			foreach (Projectile p in Main.projectile) {
				if (p != null && p.active) {
					p.active = false;
					projectiles++;
				}
			}
			foreach (Item item in Main.item) {
				if (item != null && item.active) {
					item.active = false;
					items++;
				}
			}
			return (npcs, projectiles, items);
		}

		public static GodModePlayer LocalGodMode => Main.LocalPlayer?.GetModPlayer<GodModePlayer>();
	}
}
