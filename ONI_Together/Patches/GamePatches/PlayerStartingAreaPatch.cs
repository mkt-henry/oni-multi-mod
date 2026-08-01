using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Networking;
using ONI_Together.Networking.Ownership;
using Shared.Profiling;

namespace ONI_Together.Patches.GamePatches
{
	/// <summary>
	/// Makes sure every player in the session owns a printing pod, placing one if not.
	/// </summary>
	/// <remarks>
	/// Written as a reconciliation pass rather than as a reaction to a join event, deliberately. This
	/// session has already produced two bugs from hooking the wrong moment - reporting a restore where
	/// it could not be observed, and gating on a session that was not up yet. A loop that asks "does
	/// everyone have a pod" repeatedly does not care when players arrive, when the world finishes
	/// loading, or in what order those happen.
	/// <para>
	/// Idempotent by construction: a player who already owns a pod is skipped, so running it again
	/// costs nothing and cannot produce a second pod for the same person.
	/// </para>
	/// </remarks>
	[HarmonyPatch(typeof(Game), "OnSpawn")]
	public static class PlayerStartingAreaPatch
	{
		/// <summary>
		/// Long enough that a stamp finishes before the next look, since stamping is asynchronous.
		/// </summary>
		private const float IntervalSeconds = 5f;

		public static void Postfix()
		{
			// Not gated on being the host here: hosting is not established until after the save
			// finishes loading, so asking now would answer no even when hosting.
			Schedule();
		}

		private static void Schedule()
		{
			GameScheduler.Instance?.Schedule("EnsurePlayerPods", IntervalSeconds, _ => Reconcile());
		}

		private static void Reconcile()
		{
			using var _ = Profiler.Scope();

			// Keep looking as long as a world is loaded; the session may come up later, or a player
			// may join at any point.
			if (Game.Instance == null)
				return;

			Schedule();

			if (!MultiplayerSession.IsHostInSession)
				return;

			if (StartingAreaPlacer.IsBusy)
				return;

			// Wait until at least one pod has an owner. Ownership is restored a moment after the world
			// loads, and acting before that would read the existing pod as unclaimed and stamp a
			// second one for the player who already has it.
			if (!AnyPodOwned())
				return;

			try
			{
				foreach (var player in MultiplayerSession.AllPlayers)
				{
					if (player == null || !player.IsConnected)
						continue;

					var owner = new PlayerId(player.PlayerId);
					if (!owner.IsValid || OwnsAPod(owner))
						continue;

					DebugConsole.Log($"[StartingArea] {owner} has no printing pod; placing one.");

					// One at a time. The next pass picks up anyone still waiting.
					StartingAreaPlacer.TryPlaceFor(owner);
					return;
				}
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning($"[StartingArea] Reconcile failed: {ex}");
			}
		}

		private static bool AnyPodOwned()
		{
			foreach (var record in OwnershipRegistry.Instance.All)
			{
				if (record.Type == OwnershipType.PrintingPod)
					return true;
			}

			return false;
		}

		private static bool OwnsAPod(PlayerId owner)
		{
			foreach (var record in OwnershipRegistry.Instance.ByOwner(owner))
			{
				if (record.Type == OwnershipType.PrintingPod)
					return true;
			}

			return false;
		}
	}
}
