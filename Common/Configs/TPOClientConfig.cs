using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TerrariaPerformanceOverhaul.Common.Configs
{
	/// <summary>
	/// Client-side TPO settings. Defaults favor stability: everything optional is off.
	/// Only settings for features that actually exist belong here (no placeholders for future optimizations).
	/// Values are read on the main thread when used, so changes apply on the next frame without reload.
	/// </summary>
	public sealed class TPOClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[Header("Diagnostics")]

		/// <summary>Master switch: frame-time window, memory indicators, overlay, /tpo stats and /tpo bench.</summary>
		[DefaultValue(false)]
		public bool EnableDiagnostics;

		/// <summary>Compact in-game overlay. Requires <see cref="EnableDiagnostics"/>. Text is refreshed twice per second.</summary>
		[DefaultValue(false)]
		public bool ShowOverlay;

		// Not serialized (non-public). Used to log only real changes, not every OnChanged call.
		private string lastLogged;

		public override void OnChanged() {
			string state = $"EnableDiagnostics = {EnableDiagnostics}, ShowOverlay = {ShowOverlay}";
			if (state == lastLogged)
				return;

			lastLogged = state;
			Mod?.Logger.Info($"Config: {state}");
		}
	}
}
