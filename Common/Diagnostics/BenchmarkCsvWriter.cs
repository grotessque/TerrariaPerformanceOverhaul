using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Writes a finished benchmark to three CSV files (schema: Documentation/Diagnostics/CsvSchema.md):
	///   &lt;base&gt;_summary.csv  key,value — metadata and computed results
	///   &lt;base&gt;_frames.csv   one row per recorded frame
	///   &lt;base&gt;_memory.csv   one row per memory sample (about 1 Hz)
	/// Numbers use the invariant culture. Called once per run, after recording has ended (never per frame).
	/// </summary>
	public static class BenchmarkCsvWriter
	{
		/// <summary>Increment when columns or keys change meaning.</summary>
		public const int SchemaVersion = 2;

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		public static string Write(BenchmarkSession session, List<KeyValuePair<string, string>> results, string directory) {
			Directory.CreateDirectory(directory);

			string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", Inv);
			string commit = TPOBuildIdentity.Current?.CommitDisplay ?? TPOBuildIdentity.Unknown;
			string basePath = Path.Combine(directory, $"{stamp}_{session.Scenario}_{commit}");

			WriteSummary(session, results, basePath + "_summary.csv");
			WriteFrames(session, basePath + "_frames.csv");
			WriteMemory(session, basePath + "_memory.csv");
			return basePath;
		}

		/// <summary>Computes result rows from the recorded samples. Allocates (sorted copies); end-of-run only.</summary>
		public static List<KeyValuePair<string, string>> ComputeResults(BenchmarkSession s) {
			var r = new List<KeyValuePair<string, string>>();
			void Add(string k, object v) => r.Add(new(k, Format(v)));

			float[] frames = SortedWithoutNaN(s.FrameTimes, includeZero: true);
			FrameStats stats = FrameStats.FromSorted(frames);

			Add("duration_recorded_s", s.RecordedMs / 1000.0);
			Add("truncated", s.Truncated);
			Add("frames", stats.Count);
			Add("fps_avg", stats.AvgFps);
			AddDistribution(r, "frame_ms", stats);
			Add("frames_over_25ms", FrameStats.CountAbove(frames, 25f));
			Add("frames_over_33_3ms", FrameStats.CountAbove(frames, 1000f / 30f));
			Add("frames_over_2x_p50", double.IsNaN(stats.P50Ms) ? double.NaN : FrameStats.CountAbove(frames, (float)(2 * stats.P50Ms)));

			// Draw CPU time per frame (frames where OnPreDraw was seen).
			float[] draw = SortedWithoutNaN(s.DrawCpuTimes, includeZero: true);
			Add("draw_cpu_frames", draw.Length);
			AddDistribution(r, "draw_cpu_ms", FrameStats.FromSorted(draw));

			// World update time. Per frame: sum of the updates in that frame (frames with >= 1 update).
			ReadOnlySpan<float> upd = s.UpdateTimes;
			ReadOnlySpan<byte> perFrame = s.UpdatesPerFrame;
			var updList = new List<float>(upd.Length);
			double updTotalMs = 0;
			for (int i = 0; i < upd.Length; i++) {
				if (perFrame[i] == 0)
					continue;
				updList.Add(upd[i]);
				updTotalMs += upd[i];
			}
			float[] updSorted = updList.ToArray();
			Array.Sort(updSorted);
			Add("updates", s.UpdateCount);
			Add("updates_per_s", s.RecordedMs > 0 ? s.UpdateCount / (s.RecordedMs / 1000.0) : double.NaN);
			Add("frames_with_updates", updSorted.Length);
			Add("frames_with_multiple_updates", CountMultiple(perFrame));
			AddDistribution(r, "update_ms_per_frame", FrameStats.FromSorted(updSorted));
			Add("update_ms_per_update_mean", s.UpdateCount > 0 ? updTotalMs / s.UpdateCount : double.NaN);

			// Per-category world update time.
			for (int c = 0; c < UpdateCategories.Count; c++) {
				double ms = s.CategoryMs((UpdateCategory)c);
				Add($"update_{UpdateCategories.Names[c]}_ms_total", ms);
				Add($"update_{UpdateCategories.Names[c]}_ms_per_update", s.UpdateCount > 0 ? ms / s.UpdateCount : double.NaN);
			}

			Add("unfocused_frames", s.UnfocusedFrames);
			Add("paused_frames", s.PausedFrames);

			ReadOnlySpan<MemorySample> mem = s.MemorySamples;
			if (mem.Length >= 2) {
				MemorySample first = mem[0], last = mem[mem.Length - 1];
				double seconds = last.TimeS - first.TimeS;
				long allocated = last.AllocatedBytesTotal - first.AllocatedBytesTotal;
				Add("alloc_bytes", allocated);
				Add("alloc_bytes_per_s", seconds > 0 ? allocated / seconds : double.NaN);
				Add("gen0_collections", last.Gen0Collections - first.Gen0Collections);
				Add("gen1_collections", last.Gen1Collections - first.Gen1Collections);
				Add("gen2_collections", last.Gen2Collections - first.Gen2Collections);
				Add("gc_pause_ms", last.GcPauseMsTotal - first.GcPauseMsTotal);
				Add("managed_heap_bytes_start", first.ManagedHeapBytes);
				Add("managed_heap_bytes_end", last.ManagedHeapBytes);
				Add("working_set_bytes_start", first.WorkingSetBytes);
				Add("working_set_bytes_end", last.WorkingSetBytes);
				Add("working_set_bytes_max", MaxWorkingSet(mem));
			}

			Add("tpo_diagnostics_ms_total", s.TpoMs);
			Add("tpo_diagnostics_us_per_frame", stats.Count > 0 ? s.TpoMs * 1000.0 / stats.Count : double.NaN);
			return r;
		}

		private static void AddDistribution(List<KeyValuePair<string, string>> r, string prefix, FrameStats st) {
			r.Add(new($"{prefix}_mean", Format(st.MeanMs)));
			r.Add(new($"{prefix}_min", Format(st.MinMs)));
			r.Add(new($"{prefix}_p50", Format(st.P50Ms)));
			r.Add(new($"{prefix}_p95", Format(st.P95Ms)));
			r.Add(new($"{prefix}_p99", Format(st.P99Ms)));
			r.Add(new($"{prefix}_max", Format(st.MaxMs)));
		}

		private static float[] SortedWithoutNaN(ReadOnlySpan<float> values, bool includeZero) {
			var list = new List<float>(values.Length);
			foreach (float v in values)
				if (!float.IsNaN(v) && (includeZero || v != 0))
					list.Add(v);
			float[] a = list.ToArray();
			Array.Sort(a);
			return a;
		}

		private static int CountMultiple(ReadOnlySpan<byte> perFrame) {
			int n = 0;
			foreach (byte b in perFrame)
				if (b > 1)
					n++;
			return n;
		}

		private static void WriteSummary(BenchmarkSession s, List<KeyValuePair<string, string>> results, string path) {
			var sb = new StringBuilder();
			sb.Append("key,value\n");
			foreach (var kv in s.Metadata.Concat(results))
				sb.Append(Escape(kv.Key)).Append(',').Append(Escape(kv.Value)).Append('\n');
			File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
		}

		private static void WriteFrames(BenchmarkSession s, string path) {
			using var w = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16);
			w.Write("frame_index,t_end_ms,frame_ms,draw_cpu_ms,update_ms,updates\n");
			ReadOnlySpan<float> ms = s.FrameTimes;
			ReadOnlySpan<float> draw = s.DrawCpuTimes;
			ReadOnlySpan<float> upd = s.UpdateTimes;
			ReadOnlySpan<byte> n = s.UpdatesPerFrame;
			ReadOnlySpan<double> t = s.FrameEndTimes;
			for (int i = 0; i < ms.Length; i++) {
				w.Write(i.ToString(Inv));
				w.Write(',');
				w.Write(t[i].ToString("F3", Inv));
				w.Write(',');
				w.Write(ms[i].ToString("F4", Inv));
				w.Write(',');
				if (!float.IsNaN(draw[i]))
					w.Write(draw[i].ToString("F4", Inv));
				w.Write(',');
				w.Write(upd[i].ToString("F4", Inv));
				w.Write(',');
				w.Write(n[i].ToString(Inv));
				w.Write('\n');
			}
		}

		private static void WriteMemory(BenchmarkSession s, string path) {
			using var w = new StreamWriter(path, false, new UTF8Encoding(false));
			w.Write("t_s,managed_heap_bytes,allocated_bytes_total,gen0_collections,gen1_collections,gen2_collections,gc_pause_ms_total,working_set_bytes\n");
			foreach (MemorySample m in s.MemorySamples) {
				w.Write(string.Join(",",
					m.TimeS.ToString("F3", Inv),
					m.ManagedHeapBytes.ToString(Inv),
					m.AllocatedBytesTotal.ToString(Inv),
					m.Gen0Collections.ToString(Inv),
					m.Gen1Collections.ToString(Inv),
					m.Gen2Collections.ToString(Inv),
					m.GcPauseMsTotal.ToString("F3", Inv),
					m.WorkingSetBytes.ToString(Inv)));
				w.Write('\n');
			}
		}

		private static long MaxWorkingSet(ReadOnlySpan<MemorySample> mem) {
			long max = 0;
			foreach (MemorySample m in mem)
				max = Math.Max(max, m.WorkingSetBytes);
			return max;
		}

		private static string Format(object v) => v switch {
			double d when double.IsNaN(d) => "",
			double d => d.ToString("F4", Inv),
			float f when float.IsNaN(f) => "",
			float f => f.ToString("F4", Inv),
			IFormattable f => f.ToString(null, Inv),
			null => "",
			_ => v.ToString(),
		};

		private static string Escape(string s) {
			if (string.IsNullOrEmpty(s))
				return "";
			if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
				return s;
			return "\"" + s.Replace("\"", "\"\"") + "\"";
		}
	}
}
