using System;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Distribution of frame times. Percentiles use the nearest-rank method on sorted samples:
	/// P(p) = sorted[ceil(p * n) - 1]. Percentiles are only reported when n is large enough to be meaningful.
	/// </summary>
	public readonly struct FrameStats
	{
		/// <summary>Minimum samples before p95 is reported (at least 20 samples above it).</summary>
		public const int MinSamplesP95 = 20;

		/// <summary>Minimum samples before p99 is reported (at least 1 sample above it).</summary>
		public const int MinSamplesP99 = 100;

		public int Count { get; init; }
		public double MeanMs { get; init; }
		public double MinMs { get; init; }
		public double P50Ms { get; init; }
		/// <summary>NaN when <see cref="Count"/> &lt; <see cref="MinSamplesP95"/>.</summary>
		public double P95Ms { get; init; }
		/// <summary>NaN when <see cref="Count"/> &lt; <see cref="MinSamplesP99"/>.</summary>
		public double P99Ms { get; init; }
		public double MaxMs { get; init; }

		/// <summary>Average FPS over the samples: 1000 / mean frame time.</summary>
		public double AvgFps => MeanMs > 0 ? 1000.0 / MeanMs : 0;

		public static readonly FrameStats Empty = new() {
			MeanMs = double.NaN, MinMs = double.NaN, P50Ms = double.NaN,
			P95Ms = double.NaN, P99Ms = double.NaN, MaxMs = double.NaN,
		};

		/// <summary>Computes stats from samples that are already sorted ascending. Does not allocate.</summary>
		public static FrameStats FromSorted(ReadOnlySpan<float> sorted) {
			int n = sorted.Length;
			if (n == 0)
				return Empty;

			double sum = 0;
			for (int i = 0; i < n; i++)
				sum += sorted[i];

			return new FrameStats {
				Count = n,
				MeanMs = sum / n,
				MinMs = sorted[0],
				P50Ms = NearestRank(sorted, 0.50),
				P95Ms = n >= MinSamplesP95 ? NearestRank(sorted, 0.95) : double.NaN,
				P99Ms = n >= MinSamplesP99 ? NearestRank(sorted, 0.99) : double.NaN,
				MaxMs = sorted[n - 1],
			};
		}

		/// <summary>Number of samples strictly greater than <paramref name="threshold"/> in an ascending-sorted span.</summary>
		public static int CountAbove(ReadOnlySpan<float> sorted, float threshold) {
			int lo = 0, hi = sorted.Length;
			while (lo < hi) {
				int mid = (lo + hi) >>> 1;
				if (sorted[mid] <= threshold)
					lo = mid + 1;
				else
					hi = mid;
			}
			return sorted.Length - lo;
		}

		private static double NearestRank(ReadOnlySpan<float> sorted, double p) {
			int rank = (int)Math.Ceiling(p * sorted.Length);
			return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
		}
	}
}
