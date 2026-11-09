using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TerrariaPerformanceOverhaul.Common.Configs
{
	/// <summary>
	/// Client-side TPO settings. Defaults favor stability: everything optional is off.
	/// Only settings for features that actually exist belong here (no placeholders for future optimizations).
	/// </summary>
	public sealed class TPOClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		/// <summary>
		/// Master switch for TPO diagnostics. In 0.1.0 no collectors exist yet (Plan 01),
		/// so this only records the intended state; it has no runtime cost.
		/// </summary>
		[DefaultValue(false)]
		public bool EnableDiagnostics;

		// Not serialized (non-public). Used to log only real changes, not every OnChanged call.
		private bool? lastLoggedDiagnostics;

		public override void OnChanged() {
			if (lastLoggedDiagnostics == EnableDiagnostics)
				return;

			lastLoggedDiagnostics = EnableDiagnostics;
			Mod?.Logger.Info($"Config: EnableDiagnostics = {EnableDiagnostics}");
		}
	}
}
