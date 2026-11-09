using System;
using System.Linq;
using TerrariaPerformanceOverhaul.Common.Diagnostics;

int failures = 0;
void Check(string name, bool ok) {
	Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
	if (!ok)
		failures++;
}
bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

// Nearest-rank percentiles on 1..100.
float[] hundred = Enumerable.Range(1, 100).Select(i => (float)i).ToArray();
FrameStats s = FrameStats.FromSorted(hundred);
Check("count 100", s.Count == 100);
Check("mean 50.5", Near(s.MeanMs, 50.5));
Check("p50 = 50", Near(s.P50Ms, 50));
Check("p95 = 95", Near(s.P95Ms, 95));
Check("p99 = 99", Near(s.P99Ms, 99));
Check("min/max", Near(s.MinMs, 1) && Near(s.MaxMs, 100));
Check("avg fps = 1000/mean", Near(s.AvgFps, 1000 / 50.5));

// Small samples: percentiles that are not meaningful are NaN.
FrameStats small = FrameStats.FromSorted(new float[] { 10, 20, 30 });
Check("n=3 p50 = 20", Near(small.P50Ms, 20));
Check("n=3 p95 NaN", double.IsNaN(small.P95Ms));
Check("n=3 p99 NaN", double.IsNaN(small.P99Ms));
FrameStats twenty = FrameStats.FromSorted(Enumerable.Range(1, 20).Select(i => (float)i).ToArray());
Check("n=20 p95 = 19", Near(twenty.P95Ms, 19));
Check("n=20 p99 NaN", double.IsNaN(twenty.P99Ms));
Check("empty -> Empty", FrameStats.FromSorted(ReadOnlySpan<float>.Empty).Count == 0 && double.IsNaN(FrameStats.FromSorted(ReadOnlySpan<float>.Empty).MeanMs));

// CountAbove: strictly greater.
float[] th = { 10, 16.6f, 1000f / 60f, 1000f / 60f, 17, 40 };
Array.Sort(th);
Check("count above 60fps threshold = 2", FrameStats.CountAbove(th, 1000f / 60f) == 2);
Check("count above 0 = all", FrameStats.CountAbove(th, 0) == th.Length);
Check("count above 100 = 0", FrameStats.CountAbove(th, 100) == 0);
Check("count above on empty = 0", FrameStats.CountAbove(ReadOnlySpan<float>.Empty, 1) == 0);

// Ring buffer keeps only the last N samples.
var ring = new FrameTimeRing(3);
Check("empty ring stats", ring.ComputeStats().Count == 0);
foreach (float v in new float[] { 1, 2, 3, 4 })
	ring.Add(v);
FrameStats rs = ring.ComputeStats();
Check("ring count capped at 3", rs.Count == 3);
Check("ring keeps 2,3,4", Near(rs.MinMs, 2) && Near(rs.MaxMs, 4) && Near(rs.MeanMs, 3));
ring.Add(10);
rs = ring.ComputeStats();
Check("ring after wrap keeps 3,4,10", Near(rs.MinMs, 3) && Near(rs.MaxMs, 10));
ring.Clear();
Check("ring clear", ring.Count == 0 && ring.ComputeStats().Count == 0);

Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILED");
return failures == 0 ? 0 : 1;
