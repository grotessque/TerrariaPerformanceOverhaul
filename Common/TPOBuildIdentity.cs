using System.Reflection;
using System.Runtime.InteropServices;
using Terraria.ModLoader;

namespace TerrariaPerformanceOverhaul.Common
{
	/// <summary>
	/// Immutable description of the running TPO build and its environment.
	/// Captured once on the main thread during <see cref="Mod.Load"/>; cleared in <see cref="Mod.Unload"/>.
	/// Holds only strings, so it never retains game or mod objects.
	/// </summary>
	public sealed class TPOBuildIdentity
	{
		public const string Unknown = "unknown";

		/// <summary>Identity of the currently loaded build, or null when TPO is not loaded.</summary>
		public static TPOBuildIdentity Current { get; internal set; }

		/// <summary>Mod version from build.txt.</summary>
		public string ModVersion { get; private init; }

		/// <summary>Short Git commit injected at build time (see TerrariaPerformanceOverhaul.csproj), or "unknown".</summary>
		public string Commit { get; private init; }

		/// <summary>True when the build was made from a working tree with uncommitted changes.</summary>
		public bool Dirty { get; private init; }

		/// <summary>MSBuild configuration (Debug/Release), or "unknown".</summary>
		public string Configuration { get; private init; }

		public string TmlVersion { get; private init; }
		public string TmlBranch { get; private init; }
		public string TmlPurpose { get; private init; }
		public string Runtime { get; private init; }
		public string OS { get; private init; }

		public static TPOBuildIdentity Capture(Mod mod) {
			Assembly assembly = typeof(TPOBuildIdentity).Assembly;

			// Format produced by the .NET SDK: "<version>+<SourceRevisionId>", e.g. "1.0.0+4f5a6672b1f2-dirty".
			string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			string revision = null;
			int plus = informational?.IndexOf('+') ?? -1;
			if (plus >= 0 && plus < informational.Length - 1)
				revision = informational.Substring(plus + 1);

			bool dirty = false;
			if (revision != null && revision.EndsWith("-dirty")) {
				dirty = true;
				revision = revision.Substring(0, revision.Length - "-dirty".Length);
			}

			return new TPOBuildIdentity {
				ModVersion = mod.Version?.ToString() ?? Unknown,
				Commit = string.IsNullOrEmpty(revision) ? Unknown : revision,
				Dirty = dirty,
				Configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? Unknown,
				TmlVersion = BuildInfo.tMLVersion.ToString(),
				TmlBranch = string.IsNullOrEmpty(BuildInfo.BranchName) ? Unknown : BuildInfo.BranchName,
				TmlPurpose = BuildInfo.Purpose.ToString(),
				Runtime = RuntimeInformation.FrameworkDescription,
				OS = RuntimeInformation.OSDescription,
			};
		}

		/// <summary>Commit with a "-dirty" suffix when applicable.</summary>
		public string CommitDisplay => Dirty ? Commit + "-dirty" : Commit;

		public string ToLogLine() =>
			$"TPO {ModVersion} (commit {CommitDisplay}, {Configuration}) | tModLoader {TmlVersion} ({TmlPurpose}, branch {TmlBranch}) | {Runtime} | {OS}";
	}
}
