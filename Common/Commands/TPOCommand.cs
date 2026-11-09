using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using TerrariaPerformanceOverhaul.Common.Configs;

namespace TerrariaPerformanceOverhaul.Common.Commands
{
	/// <summary>
	/// "/tpo" chat command: prints the build identity in-game, so a tester can confirm
	/// exactly which build is loaded without opening the log file.
	/// </summary>
	public sealed class TPOCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "tpo";
		public override string Usage => "/tpo";
		public override string Description => "Show TPO build identity.";

		public override void Action(CommandCaller caller, string input, string[] args) {
			TPOBuildIdentity identity = TPOBuildIdentity.Current;
			if (identity == null) {
				caller.Reply("TPO: build identity is not available.", Color.OrangeRed);
				return;
			}

			bool diagnostics = ModContent.GetInstance<TPOClientConfig>()?.EnableDiagnostics ?? false;

			caller.Reply($"TPO {identity.ModVersion} | commit {identity.CommitDisplay} | {identity.Configuration}", Color.LightGreen);
			caller.Reply($"tModLoader {identity.TmlVersion} ({identity.TmlPurpose}, branch {identity.TmlBranch})", Color.LightGray);
			caller.Reply($"{identity.Runtime} | {identity.OS}", Color.LightGray);
			caller.Reply($"Diagnostics: {(diagnostics ? "enabled" : "disabled")} (no collectors in this version)", Color.LightGray);
		}
	}
}
