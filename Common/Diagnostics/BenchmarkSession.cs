using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	public enum BenchmarkState
	{
		WarmingUp,
		Recording,
		Finished,
		Aborted,
	}

	/// <summary>Per-frame measurements passed from <see cref="DiagnosticsSystem"/> to a session.</summary>
	public readonly struct FrameSample
	{
		/// <summary>Full frame period (interval between OnPostDraw events).</summary>
		public float FrameMs { get; init; }
		/// <summary>CPU time from OnPreDraw to OnPostDraw; NaN when OnPreDraw was not seen this frame.</summary>
		public float DrawCpuMs { get; init; }
		/// <summary>Sum of world update times (PreUpdateEntities..PostUpdateEverything) of the updates in this frame.</summary>
		public float UpdateMs { get; init; }
		/// <summary>Number of world updates in this frame.</summary>
		public int Updates { get; init; }
	}

	/// <summary>
	/// One benchmark run: warm-up, then a fixed-duration recording of every frame plus 1 Hz memory samples.
	/// Buffers are allocated once at construction (before warm-up), so recording does not allocate.
	/// Main thread only.
	/// </summary>
	public sealed class BenchmarkSession
	{
		public const int MinDurationS = 5;
		public const int MaxDurationS = 600;
		public const int MaxWarmupS = 120;

		/// <summary>Frame buffer capacity per second of recording. Frames above this rate end the run early (flagged).</summary>
		public const int MaxFramesPerSecond = 1000;

		private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

		public string Scenario { get; }
		public int DurationS { get; }
		public int WarmupS { get; }
		public BenchmarkState State { get; private set; } = BenchmarkState.WarmingUp;
		public string AbortReason { get; private set; }

		/// <summary>Metadata captured at construction; end-of-run values are appended on finish.</summary>
		public List<KeyValuePair<string, string>> Metadata { get; }

		// Recording buffers.
		private readonly float[] frameMs;
		private readonly float[] drawCpuMs;
		private readonly float[] updateMs;
		private readonly byte[] updates;
		private readonly double[] frameEndMs;
		private readonly MemorySample[] memory;
		private readonly long[] categoryTicks = new long[UpdateCategories.Count];
		public int FrameCount { get; private set; }
		public int MemoryCount { get; private set; }
		public bool Truncated { get; private set; }

		// Counters during recording.
		public long UpdateCount { get; private set; }
		public int UnfocusedFrames { get; private set; }
		public int PausedFrames { get; private set; }
		/// <summary>Stopwatch ticks spent in TPO diagnostics code while recording (self-measured overhead).</summary>
		public long TpoTicks { get; set; }

		private readonly long createdTimestamp;
		private long recordStartTimestamp;
		private long nextMemoryTimestamp;
		public double RecordedMs { get; private set; }

		public BenchmarkSession(string scenario, int durationS, int warmupS) {
			Scenario = scenario;
			DurationS = Math.Clamp(durationS, MinDurationS, MaxDurationS);
			WarmupS = Math.Clamp(warmupS, 0, MaxWarmupS);

			int capacity = DurationS * MaxFramesPerSecond;
			frameMs = new float[capacity];
			drawCpuMs = new float[capacity];
			updateMs = new float[capacity];
			updates = new byte[capacity];
			frameEndMs = new double[capacity];
			memory = new MemorySample[DurationS + 2];

			Metadata = BenchmarkMetadata.CaptureEnvironment(Scenario, DurationS, WarmupS);
			createdTimestamp = Stopwatch.GetTimestamp();
		}

		public bool Recording => State == BenchmarkState.Recording;

		public ReadOnlySpan<float> FrameTimes => new(frameMs, 0, FrameCount);
		public ReadOnlySpan<float> DrawCpuTimes => new(drawCpuMs, 0, FrameCount);
		public ReadOnlySpan<float> UpdateTimes => new(updateMs, 0, FrameCount);
		public ReadOnlySpan<byte> UpdatesPerFrame => new(updates, 0, FrameCount);
		public ReadOnlySpan<double> FrameEndTimes => new(frameEndMs, 0, FrameCount);
		public ReadOnlySpan<MemorySample> MemorySamples => new(memory, 0, MemoryCount);

		public double CategoryMs(UpdateCategory c) => categoryTicks[(int)c] * MsPerTick;

		/// <summary>Seconds left in the current phase (for status display).</summary>
		public double SecondsRemaining(long now) => State switch {
			BenchmarkState.WarmingUp => WarmupS - (now - createdTimestamp) * MsPerTick / 1000.0,
			BenchmarkState.Recording => DurationS - (now - recordStartTimestamp) * MsPerTick / 1000.0,
			_ => 0,
		};

		/// <summary>Called once per drawn frame with the frame's end timestamp and its measurements.</summary>
		public void OnFrame(long now, in FrameSample sample, bool focused, bool paused) {
			if (State == BenchmarkState.WarmingUp) {
				if ((now - createdTimestamp) * MsPerTick < WarmupS * 1000.0)
					return;

				// Start recording at this frame boundary. This frame belongs to warm-up.
				State = BenchmarkState.Recording;
				recordStartTimestamp = now;
				memory[MemoryCount++] = MemorySample.Capture(0);
				nextMemoryTimestamp = now + Stopwatch.Frequency;
				return;
			}

			if (State != BenchmarkState.Recording)
				return;

			double elapsedMs = (now - recordStartTimestamp) * MsPerTick;

			if (FrameCount == frameMs.Length) {
				Truncated = true;
				Finish(now);
				return;
			}

			int i = FrameCount++;
			frameMs[i] = sample.FrameMs;
			drawCpuMs[i] = sample.DrawCpuMs;
			updateMs[i] = sample.UpdateMs;
			updates[i] = (byte)Math.Min(sample.Updates, byte.MaxValue);
			frameEndMs[i] = elapsedMs;
			if (!focused)
				UnfocusedFrames++;
			if (paused)
				PausedFrames++;

			if (now >= nextMemoryTimestamp && MemoryCount < memory.Length - 1) {
				memory[MemoryCount++] = MemorySample.Capture(elapsedMs / 1000.0);
				nextMemoryTimestamp += Stopwatch.Frequency;
			}

			if (elapsedMs >= DurationS * 1000.0)
				Finish(now);
		}

		/// <summary>Called once per world update (in-world only).</summary>
		public void OnUpdate() {
			if (State == BenchmarkState.Recording)
				UpdateCount++;
		}

		public void AddCategoryTicks(UpdateCategory c, long ticks) {
			if (State == BenchmarkState.Recording)
				categoryTicks[(int)c] += ticks;
		}

		public void Abort(string reason) {
			if (State is BenchmarkState.Finished or BenchmarkState.Aborted)
				return;

			State = BenchmarkState.Aborted;
			AbortReason = reason;
		}

		private void Finish(long now) {
			RecordedMs = (now - recordStartTimestamp) * MsPerTick;
			memory[MemoryCount++] = MemorySample.Capture(RecordedMs / 1000.0);
			State = BenchmarkState.Finished;
		}

		public double TpoMs => TpoTicks * MsPerTick;
	}
}
