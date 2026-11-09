using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Captures the context of a benchmark run as ordered key/value pairs.
	/// Main thread only (reads live game state). Called outside the measurement window.
	/// Values that cannot be read are reported as "unknown" instead of failing the run.
	/// </summary>
	public static class BenchmarkMetadata
	{
		public static List<KeyValuePair<string, string>> CaptureEnvironment(string scenario, int durationS, int warmupS) {
			var list = new List<KeyValuePair<string, string>>();
			void Add(string key, Func<object> value) => list.Add(new(key, Safe(value)));

			TPOBuildIdentity id = TPOBuildIdentity.Current;
			Add("schema_version", () => BenchmarkCsvWriter.SchemaVersion);
			Add("scenario", () => scenario);
			Add("started_local", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
			Add("started_utc", () => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
			Add("duration_requested_s", () => durationS);
			Add("warmup_s", () => warmupS);

			Add("tpo_version", () => id?.ModVersion);
			Add("tpo_commit", () => id?.CommitDisplay);
			Add("tpo_configuration", () => id?.Configuration);
			Add("tml_version", () => id?.TmlVersion);
			Add("tml_purpose", () => id?.TmlPurpose);
			Add("tml_branch", () => id?.TmlBranch);
			Add("runtime", () => id?.Runtime);
			Add("os", () => id?.OS);

			// Hardware. There is no portable CPU/GPU-name API: names come from the Windows registry (read-only, once per run).
			Add("cpu_name", WindowsHardware.CpuName);
			Add("cpu_logical_cores", () => Environment.ProcessorCount);
			Add("cpu_identifier", () => Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));
			Add("gpu_name", WindowsHardware.GpuNames);
			// FNA's adapter description. Observed in 0.2.0 to be the monitor name (e.g. "LC27G5xT"), not the GPU.
			Add("display_device", () => Main.instance?.GraphicsDevice?.Adapter?.Description);
			Add("gc_total_available_memory_bytes", () => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
			Add("gc_server", () => System.Runtime.GCSettings.IsServerGC);

			// Display and relevant settings.
			Add("resolution", () => $"{Main.screenWidth}x{Main.screenHeight}");
			Add("fullscreen", () => Main.graphics?.IsFullScreen);
			Add("vsync", () => Main.graphics?.SynchronizeWithVerticalRetrace);
			Add("frame_skip_mode", () => Main.FrameSkipMode);
			Add("lighting_mode", () => Lighting.Mode);
			Add("ui_scale", () => Main.UIScale);
			Add("game_zoom", () => Main.GameZoomTarget);

			// Scene.
			Add("world_name", () => Main.worldName);
			Add("world_size_tiles", () => $"{Main.maxTilesX}x{Main.maxTilesY}");
			Add("net_mode", () => Main.netMode);
			Add("player_tile_position", () => {
				Player p = Main.LocalPlayer;
				return p == null ? null : $"{(int)(p.Center.X / 16)},{(int)(p.Center.Y / 16)}";
			});
			Add("day_time", () => Main.dayTime);
			AddEntityCounts(list, "start");

			// Mods: name@version for every loaded mod, in load order.
			Add("mod_count", () => ModLoader.Mods.Length);
			Add("mods", () => string.Join(";", ModLoader.Mods.Select(m => $"{m.Name}@{m.Version}")));

			return list;
		}

		/// <summary>Active entity counts; a cheap indicator of scene load. Suffix distinguishes start/end.</summary>
		public static void AddEntityCounts(List<KeyValuePair<string, string>> list, string suffix) {
			list.Add(new($"active_npcs_{suffix}", Safe(() => Main.npc.Count(n => n != null && n.active))));
			list.Add(new($"active_projectiles_{suffix}", Safe(() => Main.projectile.Count(p => p != null && p.active))));
			list.Add(new($"active_items_{suffix}", Safe(() => Main.item.Count(i => i != null && i.active))));
			list.Add(new($"active_dust_{suffix}", Safe(() => Main.dust.Count(d => d != null && d.active))));
			list.Add(new($"active_gore_{suffix}", Safe(() => Main.gore.Count(g => g != null && g.active))));
		}

		internal static string Safe(Func<object> value) {
			try {
				object v = value();
				return v switch {
					null => TPOBuildIdentity.Unknown,
					IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
					_ => v.ToString(),
				};
			}
			catch (Exception) {
				return TPOBuildIdentity.Unknown;
			}
		}
	}
}
