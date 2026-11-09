using System;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Process-wide memory and GC indicators at one point in time.
	/// These values cover the whole game process (Terraria, tModLoader and every loaded mod), not TPO alone.
	/// Capture never forces a garbage collection.
	/// </summary>
	public readonly struct MemorySample
	{
		/// <summary>Seconds since the owner's reference time (for example, the start of recording).</summary>
		public double TimeS { get; init; }

		/// <summary>GC.GetTotalMemory(false): bytes currently thought to be allocated on the managed heap.</summary>
		public long ManagedHeapBytes { get; init; }

		/// <summary>GC.GetTotalAllocatedBytes(false): cumulative managed bytes allocated by the process since start.</summary>
		public long AllocatedBytesTotal { get; init; }

		public int Gen0Collections { get; init; }
		public int Gen1Collections { get; init; }
		public int Gen2Collections { get; init; }

		/// <summary>GC.GetTotalPauseDuration(): cumulative time the GC paused the process.</summary>
		public double GcPauseMsTotal { get; init; }

		/// <summary>Environment.WorkingSet: physical memory mapped to the process (native + managed).</summary>
		public long WorkingSetBytes { get; init; }

		public static MemorySample Capture(double timeS) => new() {
			TimeS = timeS,
			ManagedHeapBytes = GC.GetTotalMemory(false),
			AllocatedBytesTotal = GC.GetTotalAllocatedBytes(false),
			Gen0Collections = GC.CollectionCount(0),
			Gen1Collections = GC.CollectionCount(1),
			Gen2Collections = GC.CollectionCount(2),
			GcPauseMsTotal = GC.GetTotalPauseDuration().TotalMilliseconds,
			WorkingSetBytes = Environment.WorkingSet,
		};
	}
}
