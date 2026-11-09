namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// World update phases timed between tModLoader's ModSystem Pre/Post hooks. Order and boundaries verified from the IL of
	/// <c>Main.DoUpdateInWorld</c> in tModLoader v2026.09.3.1:
	/// PreUpdatePlayers..PostUpdatePlayers, PreUpdateNPCs..PostUpdateNPCs, PreUpdateGores..PostUpdateGores,
	/// PreUpdateProjectiles..PostUpdateProjectiles, PreUpdateItems..PostUpdateItems, PreUpdateDusts..PostUpdateDusts,
	/// PreUpdateTime..PostUpdateTime.
	///
	/// Each phase includes vanilla work and every mod's per-entity hooks for that entity type. Other mods' ModSystem hooks with the
	/// same name may run before or after TPO's and fall on either side of a boundary. This is a split by entity type, not by mod.
	/// </summary>
	public enum UpdateCategory
	{
		Players,
		NPCs,
		Gores,
		Projectiles,
		Items,
		Dusts,
		Time,
	}

	public static class UpdateCategories
	{
		public const int Count = 7;

		public static readonly string[] Names = { "players", "npcs", "gores", "projectiles", "items", "dusts", "time" };
	}
}
