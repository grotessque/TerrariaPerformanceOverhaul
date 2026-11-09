using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TerrariaPerformanceOverhaul.Common.Configs;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Owns TPO diagnostics: rolling frame-time window, CPU-side draw/update timings, memory indicators, overlay and
	/// benchmark sessions.
	///
	/// Measurement points (verified from IL, tModLoader v2026.09.3.1):
	///  - Frame period: interval between <c>Main.OnPostDraw</c> events. Raised by <c>Main.Draw_Inner</c> right after
	///    <c>Main.DoDraw</c>, once per drawn frame. Includes updates, drawing, present/vsync wait and frame limiter.
	///  - Draw CPU: <c>Main.OnPreDraw</c> (raised early in <c>Main.DoDraw</c>) to <c>Main.OnPostDraw</c>. CPU time to build and
	///    submit the frame; excludes present/vsync wait. May include driver back-pressure waits.
	///  - World update: <c>ModSystem.PreUpdateEntities</c> (in <c>Main.DoUpdate</c>, before <c>DoUpdateInWorld</c>) to
	///    <c>ModSystem.PostUpdateEverything</c> (near the end of <c>DoUpdateInWorld</c>). In-world only. Split by entity type
	///    with the Pre/Post hooks listed in <see cref="UpdateCategory"/>.
	///
	/// Threading: every member runs on the main thread (draw/update callbacks). No worker threads.
	/// Cost when diagnostics are disabled: per frame two event calls and one config read; per update about 15 virtual
	/// hook calls that only test a bool.
	/// </summary>
	public sealed class DiagnosticsSystem : ModSystem
	{
		public const int RollingWindowFrames = 1000;
		private const double RefreshIntervalS = 0.5;
		private const float OverlayScale = 0.8f;

		private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		private bool subscribed;
		private bool faulted;
		/// <summary>True while diagnostics are enabled; updated once per frame. Read by update hooks.</summary>
		private bool collecting;

		private FrameTimeRing ring;
		private long lastFrameTimestamp;
		private long nextRefreshTimestamp;

		// Per-frame accumulation.
		private long preDrawTimestamp;
		private long updateStartTimestamp;
		private long frameUpdateTicks;
		private int frameUpdates;
		private long segmentStartTimestamp;

		// Accumulation over one refresh interval (for overlay and /tpo stats).
		private long intervalDrawTicks;
		private int intervalDrawFrames;
		private long intervalUpdateTicks;
		private int intervalUpdates;

		private MemorySample lastMemory;
		private long lastMemoryTimestamp;

		public FrameStats LatestStats { get; private set; } = FrameStats.Empty;
		public MemorySample LatestMemory => lastMemory;
		/// <summary>Process-wide managed allocation rate over the last refresh interval (bytes/s); NaN before two samples.</summary>
		public double LatestAllocBytesPerS { get; private set; } = double.NaN;
		/// <summary>Mean draw CPU time per frame over the last refresh interval (ms); NaN if not measured.</summary>
		public double LatestDrawCpuMs { get; private set; } = double.NaN;
		/// <summary>Mean world update time per update over the last refresh interval (ms); NaN outside a world.</summary>
		public double LatestUpdateMs { get; private set; } = double.NaN;
		public bool Faulted => faulted;

		public BenchmarkSession Benchmark { get; private set; }
		private string overlayText;

		public static string BenchmarkDirectory => Path.Combine(Main.SavePath, "TPO", "Benchmarks");

		public override void Load() {
			if (Main.dedServ)
				return; // No drawing on a dedicated server.

			ring = new FrameTimeRing(RollingWindowFrames);
			Main.OnPreDraw += OnPreDraw;
			Main.OnPostDraw += OnPostDraw;
			subscribed = true;
		}

		public override void Unload() {
			if (subscribed) {
				Main.OnPreDraw -= OnPreDraw;
				Main.OnPostDraw -= OnPostDraw;
				subscribed = false;
			}

			collecting = false;
			ring = null;
			Benchmark = null;
			overlayText = null;
		}

		private static TPOClientConfig Config => ModContent.GetInstance<TPOClientConfig>();

		public bool Active => !faulted && ring != null && (Config?.EnableDiagnostics ?? false);

		// ---- Draw ----

		private void OnPreDraw(GameTime gameTime) {
			if (collecting)
				preDrawTimestamp = Stopwatch.GetTimestamp();
		}

		private void OnPostDraw(GameTime gameTime) {
			if (faulted)
				return;

			long now = Stopwatch.GetTimestamp();
			try {
				TPOClientConfig config = Config;
				if (config == null || !config.EnableDiagnostics) {
					if (collecting || lastFrameTimestamp != 0)
						ResetRolling();
					collecting = false;
					if (Benchmark != null)
						AbortBenchmark("diagnostics were disabled");
					return;
				}

				collecting = true;

				if (lastFrameTimestamp != 0) {
					var sample = new FrameSample {
						FrameMs = (float)((now - lastFrameTimestamp) * MsPerTick),
						DrawCpuMs = preDrawTimestamp > lastFrameTimestamp ? (float)((now - preDrawTimestamp) * MsPerTick) : float.NaN,
						UpdateMs = (float)(frameUpdateTicks * MsPerTick),
						Updates = frameUpdates,
					};

					ring.Add(sample.FrameMs);
					if (!float.IsNaN(sample.DrawCpuMs)) {
						intervalDrawTicks += now - preDrawTimestamp;
						intervalDrawFrames++;
					}
					intervalUpdateTicks += frameUpdateTicks;
					intervalUpdates += frameUpdates;

					if (Benchmark != null) {
						Benchmark.OnFrame(now, sample, Main.hasFocus, Main.gamePaused);
						if (Benchmark.State == BenchmarkState.Finished) {
							CompleteBenchmark();
							ResetFrameAccumulators();
							lastFrameTimestamp = Stopwatch.GetTimestamp(); // exclude CSV writing from the next frame
							return;
						}
					}
				}

				ResetFrameAccumulators();
				lastFrameTimestamp = now;

				if (now >= nextRefreshTimestamp) {
					nextRefreshTimestamp = now + (long)(RefreshIntervalS * Stopwatch.Frequency);
					Refresh(now, config.ShowOverlay);
				}

				if (Benchmark?.Recording == true)
					Benchmark.TpoTicks += Stopwatch.GetTimestamp() - now;
			}
			catch (Exception e) {
				Fault(e);
			}
		}

		private void ResetFrameAccumulators() {
			preDrawTimestamp = 0;
			frameUpdateTicks = 0;
			frameUpdates = 0;
		}

		public override void PostDrawInterface(SpriteBatch spriteBatch) {
			if (faulted || overlayText == null)
				return;

			long start = Stopwatch.GetTimestamp();
			try {
				TPOClientConfig config = Config;
				if (config == null || !config.EnableDiagnostics || !config.ShowOverlay)
					return;

				// Direct DynamicSpriteFont drawing: no chat-markup parsing and 2 passes (shadow + text).
				// Utils.DrawBorderString was measured at ~88 us/frame and +3.1 MB/s in 0.2.0 because it goes through
				// ChatManager.DrawColorCodedStringWithShadow (parses the text every call, draws many border passes).
				// The interface sprite batch is scaled by UIScale, so positions are in UI units.
				DynamicSpriteFont font = FontAssets.MouseText.Value;
				float uiWidth = Main.screenWidth / Main.UIScale;
				float uiHeight = Main.screenHeight / Main.UIScale;
				var position = new Vector2(uiWidth - 380f, uiHeight - 130f);

				spriteBatch.DrawString(font, overlayText, position + new Vector2(1.5f, 1.5f), Color.Black * 0.85f, 0f, Vector2.Zero, OverlayScale, SpriteEffects.None, 0f);
				spriteBatch.DrawString(font, overlayText, position, Color.White, 0f, Vector2.Zero, OverlayScale, SpriteEffects.None, 0f);

				if (Benchmark?.Recording == true)
					Benchmark.TpoTicks += Stopwatch.GetTimestamp() - start;
			}
			catch (Exception e) {
				Fault(e);
			}
		}

		// ---- World update ----
		// Hooks only read a bool and a timestamp. They are not included in TpoTicks (about 15 timestamp reads per update).

		public override void PreUpdateEntities() {
			if (collecting)
				updateStartTimestamp = Stopwatch.GetTimestamp();
		}

		public override void PostUpdateEverything() {
			if (!collecting)
				return;

			if (updateStartTimestamp != 0) {
				frameUpdateTicks += Stopwatch.GetTimestamp() - updateStartTimestamp;
				frameUpdates++;
				updateStartTimestamp = 0;
			}
			Benchmark?.OnUpdate();
		}

		public override void PreUpdatePlayers() => BeginSegment();
		public override void PostUpdatePlayers() => EndSegment(UpdateCategory.Players);
		public override void PreUpdateNPCs() => BeginSegment();
		public override void PostUpdateNPCs() => EndSegment(UpdateCategory.NPCs);
		public override void PreUpdateGores() => BeginSegment();
		public override void PostUpdateGores() => EndSegment(UpdateCategory.Gores);
		public override void PreUpdateProjectiles() => BeginSegment();
		public override void PostUpdateProjectiles() => EndSegment(UpdateCategory.Projectiles);
		public override void PreUpdateItems() => BeginSegment();
		public override void PostUpdateItems() => EndSegment(UpdateCategory.Items);
		public override void PreUpdateDusts() => BeginSegment();
		public override void PostUpdateDusts() => EndSegment(UpdateCategory.Dusts);
		public override void PreUpdateTime() => BeginSegment();
		public override void PostUpdateTime() => EndSegment(UpdateCategory.Time);

		private void BeginSegment() {
			if (collecting && Benchmark?.Recording == true)
				segmentStartTimestamp = Stopwatch.GetTimestamp();
		}

		private void EndSegment(UpdateCategory category) {
			if (segmentStartTimestamp == 0)
				return;

			long ticks = Stopwatch.GetTimestamp() - segmentStartTimestamp;
			segmentStartTimestamp = 0;
			Benchmark?.AddCategoryTicks(category, ticks);
		}

		public override void OnWorldUnload() {
			if (Benchmark != null)
				AbortBenchmark("the world was unloaded");
		}

		// ---- Rolling stats and overlay ----

		private void Refresh(long now, bool buildOverlay) {
			LatestStats = ring.ComputeStats();
			LatestDrawCpuMs = intervalDrawFrames > 0 ? intervalDrawTicks * MsPerTick / intervalDrawFrames : double.NaN;
			LatestUpdateMs = intervalUpdates > 0 ? intervalUpdateTicks * MsPerTick / intervalUpdates : double.NaN;
			intervalDrawTicks = 0;
			intervalDrawFrames = 0;
			intervalUpdateTicks = 0;
			intervalUpdates = 0;

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
				"TPO  {0:0} FPS   frame mean {1:0.00} ms\n" +
				"p50 {2:0.0}  p95 {3:0.0}  p99 {4:0.0}  max {5:0.0} ms\n" +
				"cpu: draw {6:0.00} ms  update {7:0.00} ms\n" +
				"alloc {8:0.0} MB/s  heap {9:0} MB  GC {10}/{11}/{12}",
				s.AvgFps, s.MeanMs, s.P50Ms, s.P95Ms, s.P99Ms, s.MaxMs,
				LatestDrawCpuMs, LatestUpdateMs,
				LatestAllocBytesPerS / (1024 * 1024), m.ManagedHeapBytes / (1024.0 * 1024.0),
				m.Gen0Collections, m.Gen1Collections, m.Gen2Collections);

			if (Benchmark != null)
				text += string.Format(Inv, "\nbench '{0}': {1} {2:0}s", Benchmark.Scenario, Benchmark.State, Math.Max(0, Benchmark.SecondsRemaining(now)));

			return text;
		}

		private void ResetRolling() {
			ring?.Clear();
			lastFrameTimestamp = 0;
			lastMemoryTimestamp = 0;
			ResetFrameAccumulators();
			updateStartTimestamp = 0;
			segmentStartTimestamp = 0;
			intervalDrawTicks = 0;
			intervalDrawFrames = 0;
			intervalUpdateTicks = 0;
			intervalUpdates = 0;
			LatestStats = FrameStats.Empty;
			LatestAllocBytesPerS = double.NaN;
			LatestDrawCpuMs = double.NaN;
			LatestUpdateMs = double.NaN;
			overlayText = null;
		}

		// ---- Benchmarks ----

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
			string summary = $"{session.FrameCount} frames, {Find(results, "fps_avg")} FPS avg, frame p95 {Find(results, "frame_ms_p95")} ms, " +
				$"draw CPU mean {Find(results, "draw_cpu_ms_mean")} ms, update mean {Find(results, "update_ms_per_update_mean")} ms";

			try {
				string basePath = BenchmarkCsvWriter.Write(session, results, BenchmarkDirectory);
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

		private static string Find(List<KeyValuePair<string, string>> rows, string key) {
			foreach (var kv in rows)
				if (kv.Key == key)
					return double.TryParse(kv.Value, NumberStyles.Float, Inv, out double d) ? d.ToString("0.00", Inv) : kv.Value;
			return "?";
		}

		/// <summary>Disables diagnostics for the rest of this session instead of breaking the game's draw loop.</summary>
		private void Fault(Exception e) {
			faulted = true;
			collecting = false;
			Benchmark = null;
			overlayText = null;
			Mod.Logger.Error("Diagnostics disabled after an unexpected error. Reload mods to re-enable.", e);
		}
	}
}
