using System;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Fixed-size rolling window of the most recent frame times (ms).
	/// All memory is allocated in the constructor; <see cref="Add"/> and <see cref="ComputeStats"/> do not allocate.
	/// Main thread only.
	/// </summary>
	public sealed class FrameTimeRing
	{
		private readonly float[] samples;
		private readonly float[] scratch;
		private int count;
		private int next;

		public FrameTimeRing(int capacity) {
			if (capacity <= 0)
				throw new ArgumentOutOfRangeException(nameof(capacity));

			samples = new float[capacity];
			scratch = new float[capacity];
		}

		public int Count => count;

		public void Add(float frameMs) {
			samples[next] = frameMs;
			next = (next + 1) % samples.Length;
			if (count < samples.Length)
				count++;
		}

		public void Clear() {
			count = 0;
			next = 0;
		}

		/// <summary>Sorts a copy of the window (O(n log n), n = capacity). Call at a low rate, not per frame.</summary>
		public FrameStats ComputeStats() {
			if (count == 0)
				return FrameStats.Empty;

			// Order inside the window does not matter for distribution stats.
			Array.Copy(samples, scratch, count);
			Array.Sort(scratch, 0, count);
			return FrameStats.FromSorted(new ReadOnlySpan<float>(scratch, 0, count));
		}
	}
}
