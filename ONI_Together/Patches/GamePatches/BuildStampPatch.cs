using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Misc;
using ONI_Together.Networking;

namespace ONI_Together.Patches.GamePatches
{
	/// <summary>
	/// Puts the build identity into the log of every machine in a session.
	/// </summary>
	/// <remarks>
	/// Printed once per world load, next to the local and host ids, so that comparing two logs answers
	/// "were these the same build" before anyone starts explaining a difference in behaviour some
	/// other way. A build mismatch does not announce itself - it looks like desync.
	/// </remarks>
	[HarmonyPatch(typeof(Game), "OnSpawn")]
	public static class BuildStampPatch
	{
		private static bool _loggedThisWorld;

		public static void Postfix()
		{
			if (_loggedThisWorld) return;
			_loggedThisWorld = true;

			DebugConsole.Log(
				$"[Build] {BuildStamp.Value} | local={MultiplayerSession.LocalUserID} " +
				$"host={MultiplayerSession.HostUserID} inSession={MultiplayerSession.InSession}");
		}

		/// <summary>Called when a world unloads so the next one logs again.</summary>
		public static void Reset() => _loggedThisWorld = false;
	}
}
