using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TerrariaPerformanceOverhaul.Common.Configs;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Owns TPO diagnostics: rolling frame-time window, memory indicators, overlay and benchmark sessions.
	///
	/// Frame time source: <c>Main.OnPostDraw</c> (static <c>Action&lt;GameTime&gt;</c>). In tModLoader v2026.09.3.1 it is
	/// raised by <c>Main.Draw_Inner</c> right after <c>Main.DoDraw</c>, once per drawn frame (verified from IL).
	/// The interval between two consecutive calls is the full frame period: game updates + draw + present + frame limiter.
	///
	/// Threading: every member runs on the main thread (draw/update callbacks). No worker threads.
	/// Cost when diagnostics are disabled: one event call and one config read per frame.
	/// </summary>
	public sealed class DiagnosticsSystem : ModSystem
	{
		public const int RollingWindowFrames = 1000;
		private const double RefreshIntervalS = 0.5;

		private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		private bool subscribed;
		private bool faulted;
		private FrameTimeRing ring;
		private long lastFrameTimestamp;
		private long nextRefreshTimestamp;

		private MemorySample lastMemory;
		private long lastMemoryTimestamp;

		public FrameStats LatestStats { get; private set; } = FrameStats.Empty;
		public MemorySample LatestMemory => lastMemory;
		/// <summary>Process-wide managed allocation rate over the last refresh interval (bytes/s); NaN before two samples.</summary>
		public double LatestAllocBytesPerS { get; private set; } = double.NaN;
		public bool Faulted => faulted;

		public BenchmarkSession Benchmark { get; private set; }
		private string overlayText;

		public static string BenchmarkDirectory => Path.Combine(Main.SavePath, "TPO", "Benchmarks");

		public override void Load() {
			if (Main.dedServ)
				return; // No drawing on a dedicated server.

			ring = new FrameTimeRing(RollingWindowFrames);
			Main.OnPostDraw += OnPostDraw;
			subscribed = true;
		}

		public override void Unload() {
			if (subscribed) {
				Main.OnPostDraw -= OnPostDraw;
				subscribed = false;
			}

			ring = null;
			Benchmark = null;
			overlayText = null;
		}

		private static TPOClientConfig Config => ModContent.GetInstance<TPOClientConfig>();

		public bool Active => !faulted && ring != null && (Config?.EnableDiagnostics ?? false);

		private void OnPostDraw(GameTime gameTime) {
			if (faulted)
				return;

			long now = Stopwatch.GetTimestamp();
			try {
				TPOClientConfig config = Config;
				if (config == null || !config.EnableDiagnostics) {
					if (lastFrameTimestamp != 0)
						ResetRolling();
					if (Benchmark != null)
						AbortBenchmark("diagnostics were disabled");
					return;
				}

				if (lastFrameTimestamp != 0) {
					float ms = (float)((now - lastFrameTimestamp) * MsPerTick);
					ring.Add(ms);

					if (Benchmark != null) {
						Benchmark.OnFrame(now, ms, Main.hasFocus, Main.gamePaused);
						if (Benchmark.State == BenchmarkState.Finished) {
							CompleteBenchmark();
							lastFrameTimestamp = Stopwatch.GetTimestamp(); // exclude CSV writing from the next frame
							return;
						}
					}
				}
				lastFrameTimestamp = now;

				if (now >= nextRefreshTimestamp) {
					nextRefreshTimestamp = now + (long)(RefreshIntervalS * Stopwatch.Frequency);
					Refresh(now, config.ShowOverlay);
				}

				if (Benchmark?.State == BenchmarkState.Recording)
					Benchmark.TpoTicks += Stopwatch.GetTimestamp() - now;
			}
			catch (Exception e) {
				Fault(e);
			}
		}

		public override void PostUpdateEverything() {
			Benchmark?.OnUpdate();
		}

		public override void OnWorldUnload() {
			if (Benchmark != null)
				AbortBenchmark("the world was unloaded");
		}

		public override void PostDrawInterface(SpriteBatch spriteBatch) {
			if (faulted || overlayText == null)
				return;

			long start = Stopwatch.GetTimestamp();
			try {
				TPOClientConfig config = Config;
				if (config == null || !config.EnableDiagnostics || !config.ShowOverlay)
					return;

				// The interface sprite batch is scaled by UIScale, so positions are in UI units.
				float uiWidth = Main.screenWidth / Main.UIScale;
				float uiHeight = Main.screenHeight / Main.UIScale;
				var position = new Vector2(uiWidth - 360f, uiHeight - 110f);
				Utils.DrawBorderString(spriteBatch, overlayText, position, Color.White, 0.8f);

				if (Benchmark?.State == BenchmarkState.Recording)
					Benchmark.TpoTicks += Stopwatch.GetTimestamp() - start;
			}
			catch (Exception e) {
				Fault(e);
			}
		}

		private void Refresh(long now, bool buildOverlay) {
			LatestStats = ring.ComputeStats();

			MemorySample mem = MemorySample.Capture(0);
			if (lastMemoryTimestamp != 0) {
				double seconds = (now - lastMemoryTimestamp) * MsPerTick / 1000.0;
				LatestAllocBytesPerS = seconds > 0 ? (mem.AllocatedBytesTotal - lastMemory.AllocatedBytesTotal) / seconds : double.NaN;
			}
			lastMemory = mem;
			lastMemoryTimestamp = now;

			// The overlay string is rebuilt at most twice per second (one small allocation), never per frame.
			overlayText = buildOverlay ? BuildOverlayText(now) : null;
		}

		private string BuildOverlayText(long now) {
			FrameStats s = LatestStats;
			MemorySample m = lastMemory;
			string text = string.Format(Inv,
				"TPO  {0:0} FPS   mean {1:0.00} ms\n" +
				"p50 {2:0.0}  p95 {3:0.0}  p99 {4:0.0}  max {5:0.0} ms\n" +
				"alloc {6:0.0} MB/s  heap {7:0} MB  GC {8}/{9}/{10}",
				s.AvgFps, s.MeanMs, s.P50Ms, s.P95Ms, s.P99Ms, s.MaxMs,
				LatestAllocBytesPerS / (1024 * 1024), m.ManagedHeapBytes / (1024.0 * 1024.0),
				m.Gen0Collections, m.Gen1Collections, m.Gen2Collections);

			if (Benchmark != null)
				text += string.Format(Inv, "\nbench '{0}': {1} {2:0}s", Benchmark.Scenario, Benchmark.State, Math.Max(0, Benchmark.SecondsRemaining(now)));

			return text;
		}

		private void ResetRolling() {
			ring.Clear();
			lastFrameTimestamp = 0;
			lastMemoryTimestamp = 0;
			LatestStats = FrameStats.Empty;
			LatestAllocBytesPerS = double.NaN;
			overlayText = null;
		}

		/// <summary>Starts a benchmark. Returns null on success, otherwise the reason it cannot start.</summary>
		public string StartBenchmark(string scenario, int durationS, int warmupS) {
			if (faulted)
				return "diagnostics are disabled after an internal error (see client.log)";
			if (!Active)
				return "enable diagnostics first: Settings > Mod Configuration > TPO Client Settings";
			if (Main.gameMenu)
				return "enter a world first";
			if (Benchmark != null)
				return $"benchmark '{Benchmark.Scenario}' is already running; use /tpo bench stop";

			Benchmark = new BenchmarkSession(scenario, durationS, warmupS);
			Mod.Logger.Info($"Benchmark '{Benchmark.Scenario}' started: warm-up {Benchmark.WarmupS}s, duration {Benchmark.DurationS}s.");
			return null;
		}

		public void AbortBenchmark(string reason) {
			if (Benchmark == null)
				return;

			Benchmark.Abort(reason);
			Mod.Logger.Info($"Benchmark '{Benchmark.Scenario}' aborted: {reason}. No files were written.");
			if (!Main.dedServ && !Main.gameMenu)
				Main.NewText($"TPO: benchmark '{Benchmark.Scenario}' aborted: {reason}.", Color.OrangeRed);
			Benchmark = null;
		}

		private void CompleteBenchmark() {
			BenchmarkSession session = Benchmark;
			Benchmark = null;

			BenchmarkMetadata.AddEntityCounts(session.Metadata, "end");
			var results = BenchmarkCsvWriter.ComputeResults(session);
			string summary = $"{session.FrameCount} frames, {Find(results, "fps_avg")} FPS avg, p95 {Find(results, "frame_ms_p95")} ms, p99 {Find(results, "frame_ms_p99")} ms";

			try {
				string basePath = BenchmarkCsvWriter.Write(session, BenchmarkDirectory);
				Mod.Logger.Info($"Benchmark '{session.Scenario}' finished: {summary}. Files: {basePath}_*.csv");
				Main.NewText($"TPO: benchmark '{session.Scenario}' finished: {summary}.", Color.LightGreen);
				Main.NewText($"TPO: saved {Path.GetFileName(basePath)}_*.csv in {BenchmarkDirectory}", Color.LightGray);
				if (session.Truncated)
					Main.NewText($"TPO: recording stopped early: more than {BenchmarkSession.MaxFramesPerSecond} FPS on average.", Color.Yellow);
				if (session.UnfocusedFrames > 0 || session.PausedFrames > 0)
					Main.NewText($"TPO: warning: {session.UnfocusedFrames} unfocused / {session.PausedFrames} paused frames; results may not be comparable.", Color.Yellow);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
				Mod.Logger.Error($"Benchmark '{session.Scenario}' finished ({summary}) but CSV files could not be written.", e);
				Main.NewText($"TPO: benchmark finished but files could not be written: {e.Message}", Color.OrangeRed);
			}
		}

		private static string Find(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> rows, string key) {
			foreach (var kv in rows)
				if (kv.Key == key)
					return double.TryParse(kv.Value, NumberStyles.Float, Inv, out double d) ? d.ToString("0.0", Inv) : kv.Value;
			return "?";
		}

		/// <summary>Disables diagnostics for the rest of this session instead of breaking the game's draw loop.</summary>
		private void Fault(Exception e) {
			faulted = true;
			Benchmark = null;
			overlayText = null;
			Mod.Logger.Error("Diagnostics disabled after an unexpected error. Reload mods to re-enable.", e);
		}
	}
}
