using Terraria.ModLoader;
using TerrariaPerformanceOverhaul.Common;

namespace TerrariaPerformanceOverhaul
{
	// Mod skeleton reference: https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents
	//
	// Lifecycle rules for TPO (see CLAUDE.md):
	//  - Everything created in Load must be released in Unload (static state, hooks, events, caches).
	//  - Unload is also called when Load fails part-way, so cleanup must tolerate partially initialized state.
	public class TerrariaPerformanceOverhaul : Mod
	{
		public override void Load() {
			TPOBuildIdentity.Current = TPOBuildIdentity.Capture(this);
			Logger.Info($"Loading. {TPOBuildIdentity.Current.ToLogLine()}");
		}

		public override void Unload() {
			Logger.Info("Unloading.");
			TPOBuildIdentity.Current = null;
		}
	}
}
