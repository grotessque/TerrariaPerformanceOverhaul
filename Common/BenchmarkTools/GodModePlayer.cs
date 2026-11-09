using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TerrariaPerformanceOverhaul.Common.BenchmarkTools
{
	/// <summary>
	/// Benchmark tool: makes the local player immune to all damage, so an idle player can stay in a boss fight for a
	/// full recording. State is not saved; it resets when the player leaves the world.
	///
	/// Only damage/death hooks are used (no per-tick hooks), so the cost when god mode is off is a bool check on hit.
	/// </summary>
	public sealed class GodModePlayer : ModPlayer
	{
		public bool GodMode { get; set; }

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => GodMode;

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
			if (!GodMode)
				return true;

			// Safety net for damage that bypasses ImmuneTo.
			Player.statLife = Player.statLifeMax2;
			return false;
		}

		public override void OnEnterWorld() {
			GodMode = false;
		}
	}
}
