using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using TerrariaPerformanceOverhaul.Common.BenchmarkTools;
using TerrariaPerformanceOverhaul.Common.Configs;
using TerrariaPerformanceOverhaul.Common.Diagnostics;

namespace TerrariaPerformanceOverhaul.Common.Commands
{
	/// <summary>
	/// "/tpo" chat command.
	///   /tpo                                         build identity
	///   /tpo stats                                   rolling frame-time and memory stats
	///   /tpo bench &lt;scenario&gt; [seconds] [warmup]   start a benchmark (default 60 s, warm-up 10 s)
	///   /tpo bench stop                              abort the running benchmark
	/// Benchmark tools (config "Enable benchmark tools", single player only):
	///   /tpo god [on|off]                            immunity to all damage
	///   /tpo find &lt;text&gt;                             list NPC names (bosses first)
	///   /tpo spawn &lt;npc&gt; [count]                     spawn like a summon item; npc = id, vanilla name or Mod/Name
	///   /tpo event &lt;name|stop&gt;                       start/stop an invasion or moon event
	///   /tpo time &lt;day|noon|night|midnight&gt;
	///   /tpo clear                                   remove hostile NPCs, projectiles and items on the ground
	/// </summary>
	public sealed class TPOCommand : ModCommand
	{
		private const int DefaultDurationS = 60;
		private const int DefaultWarmupS = 10;
		private static readonly Regex ScenarioPattern = new("^[A-Za-z0-9_-]{1,40}$");
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		public override CommandType Type => CommandType.Chat;
		public override string Command => "tpo";
		public override string Usage => "/tpo [stats | bench <scenario> [seconds] [warmup] | bench stop | god | find | spawn | event | time | clear]";
		public override string Description => "TPO build identity, diagnostics and benchmarks.";

		public override void Action(CommandCaller caller, string input, string[] args) {
			string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";
			switch (sub) {
				case "":
					ShowIdentity(caller);
					break;
				case "stats":
					ShowStats(caller);
					break;
				case "bench":
					Bench(caller, args);
					break;
				case "god":
				case "find":
				case "spawn":
				case "event":
				case "time":
				case "clear":
					Tool(caller, sub, args);
					break;
				default:
					caller.Reply($"Usage: {Usage}", Color.OrangeRed);
					break;
			}
		}

		private void Tool(CommandCaller caller, string sub, string[] args) {
			string unavailable = SceneTools.CheckAvailable();
			if (unavailable != null) {
				caller.Reply($"TPO: {unavailable}.", Color.OrangeRed);
				return;
			}

			string arg1 = args.Length > 1 ? args[1].ToLowerInvariant() : "";
			string message;
			switch (sub) {
				case "god": {
					GodModePlayer god = SceneTools.LocalGodMode;
					god.GodMode = arg1 switch { "on" => true, "off" => false, _ => !god.GodMode };
					message = $"god mode {(god.GodMode ? "on" : "off")}";
					break;
				}
				case "find": {
					if (args.Length < 2) {
						caller.Reply("Usage: /tpo find <text>", Color.OrangeRed);
						return;
					}
					var found = SceneTools.Find(args[1], 15);
					if (found.Count == 0) {
						caller.Reply($"TPO: no NPC matches '{args[1]}'.", Color.LightGray);
						return;
					}
					foreach (var f in found)
						caller.Reply($"{(f.Boss ? "[boss] " : "")}{f.Internal}  ({f.Display}, id {f.Type})", f.Boss ? Color.Gold : Color.LightGray);
					return; // listing only; not logged
				}
				case "spawn": {
					if (args.Length < 2) {
						caller.Reply("Usage: /tpo spawn <npc> [count]   (find names with /tpo find <text>)", Color.OrangeRed);
						return;
					}
					if (!SceneTools.TryResolveNpc(args[1], out int type)) {
						caller.Reply($"TPO: unknown NPC '{args[1]}'. Use /tpo find <text>; modded names are 'Mod/Name'.", Color.OrangeRed);
						return;
					}
					int count = 1;
					if (args.Length > 2 && (!int.TryParse(args[2], NumberStyles.Integer, Inv, out count) || count < 1 || count > SceneTools.MaxSpawnCount)) {
						caller.Reply($"TPO: count must be 1-{SceneTools.MaxSpawnCount}.", Color.OrangeRed);
						return;
					}
					SceneTools.Spawn(type, count);
					message = $"spawned {SceneTools.InternalName(type)} x{count}";
					break;
				}
				case "event": {
					message = arg1 == "" ? null : SceneTools.StartEvent(arg1);
					if (message == null) {
						caller.Reply($"Usage: /tpo event <{SceneTools.EventNames}>", Color.OrangeRed);
						return;
					}
					break;
				}
				case "time": {
					message = SceneTools.SetTime(arg1);
					if (message == null) {
						caller.Reply("Usage: /tpo time <day|noon|night|midnight>", Color.OrangeRed);
						return;
					}
					break;
				}
				default: { // clear
					var (npcs, projectiles, items) = SceneTools.Clear();
					message = $"cleared {npcs} hostile NPCs, {projectiles} projectiles, {items} items";
					break;
				}
			}

			Mod.Logger.Info($"Benchmark tool: {message}.");
			caller.Reply($"TPO: {message}.", Color.LightGreen);
		}

		private static void ShowIdentity(CommandCaller caller) {
			TPOBuildIdentity identity = TPOBuildIdentity.Current;
			if (identity == null) {
				caller.Reply("TPO: build identity is not available.", Color.OrangeRed);
				return;
			}

			bool diagnostics = ModContent.GetInstance<TPOClientConfig>()?.EnableDiagnostics ?? false;

			caller.Reply($"TPO {identity.ModVersion} | commit {identity.CommitDisplay} | {identity.Configuration}", Color.LightGreen);
			caller.Reply($"tModLoader {identity.TmlVersion} ({identity.TmlPurpose}, branch {identity.TmlBranch})", Color.LightGray);
			caller.Reply($"{identity.Runtime} | {identity.OS}", Color.LightGray);
			caller.Reply($"Diagnostics: {(diagnostics ? "enabled" : "disabled")}", Color.LightGray);
		}

		private static void ShowStats(CommandCaller caller) {
			DiagnosticsSystem diag = ModContent.GetInstance<DiagnosticsSystem>();
			if (diag == null || !diag.Active) {
				caller.Reply(diag?.Faulted == true
					? "TPO: diagnostics are disabled after an internal error (see client.log)."
					: "TPO: enable diagnostics first: Settings > Mod Configuration > TPO Client Settings.", Color.OrangeRed);
				return;
			}

			FrameStats s = diag.LatestStats;
			MemorySample m = diag.LatestMemory;
			caller.Reply(string.Format(Inv,
				"Last {0} frames: {1:0.0} FPS avg | mean {2:0.00} ms | p50 {3:0.00} | p95 {4:0.00} | p99 {5:0.00} | max {6:0.00} ms",
				s.Count, s.AvgFps, s.MeanMs, s.P50Ms, s.P95Ms, s.P99Ms, s.MaxMs), Color.LightGreen);
			caller.Reply(string.Format(Inv,
				"CPU (last 0.5 s): draw {0:0.00} ms/frame | world update {1:0.00} ms/update",
				diag.LatestDrawCpuMs, diag.LatestUpdateMs), Color.LightGray);
			caller.Reply(string.Format(Inv,
				"Process: alloc {0:0.00} MB/s | managed heap {1:0.0} MB | working set {2:0} MB | GC {3}/{4}/{5} | GC pause total {6:0} ms",
				diag.LatestAllocBytesPerS / (1024 * 1024), m.ManagedHeapBytes / (1024.0 * 1024.0), m.WorkingSetBytes / (1024.0 * 1024.0),
				m.Gen0Collections, m.Gen1Collections, m.Gen2Collections, m.GcPauseMsTotal), Color.LightGray);
		}

		private static void Bench(CommandCaller caller, string[] args) {
			DiagnosticsSystem diag = ModContent.GetInstance<DiagnosticsSystem>();
			if (diag == null) {
				caller.Reply("TPO: diagnostics are not available.", Color.OrangeRed);
				return;
			}

			if (args.Length < 2) {
				caller.Reply("Usage: /tpo bench <scenario> [seconds] [warmup] | /tpo bench stop", Color.OrangeRed);
				return;
			}

			if (args[1].Equals("stop", StringComparison.OrdinalIgnoreCase)) {
				if (diag.Benchmark == null)
					caller.Reply("TPO: no benchmark is running.", Color.LightGray);
				else
					diag.AbortBenchmark("stopped by command");
				return;
			}

			string scenario = args[1];
			if (!ScenarioPattern.IsMatch(scenario)) {
				caller.Reply("TPO: scenario name: 1-40 characters, letters, digits, '_' or '-'.", Color.OrangeRed);
				return;
			}

			int duration = DefaultDurationS, warmup = DefaultWarmupS;
			if ((args.Length > 2 && !int.TryParse(args[2], NumberStyles.Integer, Inv, out duration))
				|| (args.Length > 3 && !int.TryParse(args[3], NumberStyles.Integer, Inv, out warmup))) {
				caller.Reply("TPO: seconds and warmup must be whole numbers.", Color.OrangeRed);
				return;
			}

			if (duration < BenchmarkSession.MinDurationS || duration > BenchmarkSession.MaxDurationS
				|| warmup < 0 || warmup > BenchmarkSession.MaxWarmupS) {
				caller.Reply($"TPO: seconds must be {BenchmarkSession.MinDurationS}-{BenchmarkSession.MaxDurationS}, warmup 0-{BenchmarkSession.MaxWarmupS}.", Color.OrangeRed);
				return;
			}

			string error = diag.StartBenchmark(scenario, duration, warmup);
			if (error != null) {
				caller.Reply($"TPO: {error}.", Color.OrangeRed);
				return;
			}

			caller.Reply($"TPO: benchmark '{scenario}': warm-up {warmup}s, then recording {duration}s. Keep the camera and scene unchanged.", Color.LightGreen);
		}
	}
}
