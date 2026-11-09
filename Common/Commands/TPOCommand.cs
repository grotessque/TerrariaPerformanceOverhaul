using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
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
	/// </summary>
	public sealed class TPOCommand : ModCommand
	{
		private const int DefaultDurationS = 60;
		private const int DefaultWarmupS = 10;
		private static readonly Regex ScenarioPattern = new("^[A-Za-z0-9_-]{1,40}$");
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		public override CommandType Type => CommandType.Chat;
		public override string Command => "tpo";
		public override string Usage => "/tpo [stats | bench <scenario> [seconds] [warmup] | bench stop]";
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
				default:
					caller.Reply($"Usage: {Usage}", Color.OrangeRed);
					break;
			}
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
