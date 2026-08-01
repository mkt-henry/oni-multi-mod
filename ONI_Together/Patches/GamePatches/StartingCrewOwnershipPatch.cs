using System.Collections.Generic;
using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Networking;
using ONI_Together.Networking.Components;
using ONI_Together.Networking.Ownership;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Patches.GamePatches
{
	/// <summary>
	/// Claims duplicants that were placed by worldgen for the owner of the pod they started next to.
	/// </summary>
	/// <remarks>
	/// Every duplicant is meant to belong to somebody. Printed ones inherit from the pod that printed
	/// them, but the starting crew is put there by worldgen with no pod involved, so nothing claimed
	/// them and they stayed open to everyone. That is a hole in the design, not a feature of it -
	/// two players able to give orders to the same duplicant is exactly what this mod exists to
	/// prevent.
	/// <para>
	/// Assigned by proximity, which is what "your starting crew" means once each player gets their own
	/// starting area: the duplicants standing in it. With a single pod this simply gives everyone to
	/// its owner.
	/// </para>
	/// <para>
	/// Runs a moment after the world loads rather than during it. Duplicants and pods spawn in no
	/// guaranteed order, and a pass that ran too early would see pods that had not claimed their own
	/// ownership yet and hand the crew to nobody.
	/// </para>
	/// </remarks>
	[HarmonyPatch(typeof(Game), "OnSpawn")]
	public static class StartingCrewOwnershipPatch
	{
		private const float SettleDelaySeconds = 2f;
		private const int MaxAttempts = 10;

		public static void Postfix()
		{
			// Deliberately not gated on being the host here. Hosting is established after the save
			// finishes loading, so at this point inSession is still false even when hosting - checking
			// now is what stopped this from ever running. The scheduled pass decides instead.
			Schedule(attempt: 1);
		}

		private static void Schedule(int attempt)
		{
			GameScheduler.Instance?.Schedule("ClaimStartingCrew", SettleDelaySeconds, _ => ClaimUnownedDuplicants(attempt));
		}

		private static void ClaimUnownedDuplicants(int attempt)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHostInSession)
			{
				// Either singleplayer, a client, or hosting has not finished coming up yet. There is no
				// event to wait on that reliably means "the session is ready", so retry a bounded
				// number of times and then stop rather than poll forever.
				if (attempt < MaxAttempts)
					Schedule(attempt + 1);

				return;
			}

			try
			{
				var pods = CollectOwnedPods();
				if (pods.Count == 0)
					return;

				int claimed = 0;

				// global:: because the mod has its own Components namespace that shadows the game's class.
				foreach (var minion in global::Components.LiveMinionIdentities.Items)
				{
					if (minion == null) continue;

					var go = minion.gameObject;
					var ownership = go.AddOrGet<OwnershipComponent>();
					if (ownership.HasOwner) continue;

					var pod = NearestPod(pods, go.transform.GetPosition());
					if (pod == null) continue;

					var identity = go.AddOrGet<NetworkIdentity>();
					if (identity.NetId == 0)
						identity.RegisterIdentity();
					if (identity.NetId == 0) continue;

					ownership.Assign(pod.Owner, OwnershipType.Duplicant, pod.WorldId);
					claimed++;
				}

				if (claimed > 0)
					DebugConsole.Log($"[StartingCrew] Claimed {claimed} previously unowned duplicant(s) for the nearest pod owner.");
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning($"[StartingCrew] Failed to claim unowned duplicants: {ex}");
			}
		}

		private static List<OwnershipComponent> CollectOwnedPods()
		{
			var pods = new List<OwnershipComponent>();

			foreach (var telepad in global::Components.Telepads.Items)
			{
				if (telepad == null) continue;
				if (telepad.TryGetComponent<OwnershipComponent>(out var ownership) && ownership.HasOwner)
					pods.Add(ownership);
			}

			return pods;
		}

		/// <summary>
		/// Nearest pod on the same asteroid, falling back to nearest overall.
		/// </summary>
		/// <remarks>
		/// World is checked first because straight-line distance is meaningless between asteroids -
		/// two points can be close in coordinates and unreachable from each other.
		/// </remarks>
		private static OwnershipComponent NearestPod(List<OwnershipComponent> pods, Vector3 position)
		{
			OwnershipComponent bestSameWorld = null;
			OwnershipComponent bestAnyWorld = null;
			float closestSameWorld = float.MaxValue;
			float closestAnyWorld = float.MaxValue;

			int worldId = Grid.IsValidCell(Grid.PosToCell(position))
				? Grid.WorldIdx[Grid.PosToCell(position)]
				: -1;

			foreach (var pod in pods)
			{
				float distance = Vector3.SqrMagnitude(pod.transform.GetPosition() - position);

				if (distance < closestAnyWorld)
				{
					closestAnyWorld = distance;
					bestAnyWorld = pod;
				}

				if (worldId >= 0 && pod.WorldId == worldId && distance < closestSameWorld)
				{
					closestSameWorld = distance;
					bestSameWorld = pod;
				}
			}

			return bestSameWorld ?? bestAnyWorld;
		}
	}
}
