using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Networking;
using ONI_Together.Networking.Components;
using ONI_Together.Networking.Ownership;
using Shared.Profiling;

namespace ONI_Together.Patches.GamePatches
{
	/// <summary>
	/// Gives every printing pod an owner.
	/// </summary>
	/// <remarks>
	/// Telepad adds itself to Components.Telepads on spawn, so the game already copes with more than
	/// one pod existing. This hooks the same moment to make sure each pod carries an
	/// <see cref="OwnershipComponent"/> and, on the host, that it belongs to somebody.
	/// <para>
	/// No packet is sent, and none is needed: ownership is serialised onto the pod, and a client
	/// receives the world as the host's save file. The ownership a client sees is therefore the same
	/// bytes the host wrote, both on join and after a hard sync. A packet only becomes necessary once
	/// ownership can change while a session is live.
	/// </para>
	/// <para>
	/// While only one pod exists it goes to the host. Assigning a pod per player is the job of the
	/// phase that places the extra pods - see <see cref="ResolveOwnerFor"/>.
	/// </para>
	/// </remarks>
	[HarmonyPatch(typeof(Telepad), "OnSpawn")]
	public static class TelepadOwnershipPatch
	{
		public static void Postfix(Telepad __instance)
		{
			using var _ = Profiler.Scope();

			// Singleplayer is left exactly as it was: no component, nothing written to the save.
			if (!MultiplayerSession.InSession) return;
			if (__instance == null) return;

			try
			{
				var pod = __instance.gameObject;

				// Ownership is keyed by NetId, so the pod needs an identity before it can be recorded.
				var identity = pod.AddOrGet<NetworkIdentity>();
				if (identity.NetId == 0)
					identity.RegisterIdentity();

				if (identity.NetId == 0)
				{
					DebugConsole.LogWarning("[TelepadOwnership] Pod has no usable NetId; leaving it unowned.");
					return;
				}

				var ownership = pod.AddOrGet<OwnershipComponent>();

				// Loaded from a save that already knew the owner. OwnershipComponent.OnSpawn has put it
				// back in the registry, so re-asserting here would only risk fighting the host.
				if (ownership.HasOwner)
				{
					DebugConsole.Log($"[TelepadOwnership] Pod netId={identity.NetId} restored for {ownership.Owner}.");
					return;
				}

				// Only the host decides ownership. A client that reaches here has a pod the host has not
				// assigned yet; it will arrive owned on the next sync.
				if (!MultiplayerSession.IsHost)
					return;

				var owner = ResolveOwnerFor(__instance);
				if (!owner.IsValid)
				{
					DebugConsole.LogWarning($"[TelepadOwnership] No owner available for pod netId={identity.NetId}; leaving it unowned.");
					return;
				}

				ownership.Assign(owner, OwnershipType.PrintingPod, ResolveWorldId(__instance));
			}
			catch (System.Exception ex)
			{
				// A pod without an owner is recoverable; a throw inside OnSpawn is not.
				DebugConsole.LogWarning($"[TelepadOwnership] Failed to assign pod ownership: {ex}");
			}
		}

		/// <summary>
		/// Picks the player a newly spawned pod belongs to.
		/// </summary>
		/// <remarks>
		/// Everything currently maps to the host, because a vanilla world generates exactly one pod.
		/// When worldgen starts stamping one starting area per player, this is the single place that
		/// has to learn how to tell them apart.
		/// </remarks>
		private static PlayerId ResolveOwnerFor(Telepad pod)
		{
			return PlayerId.Host;
		}

		private static int ResolveWorldId(Telepad pod)
		{
			// Mirrors how Telepad itself asks, rather than assuming the pod is on the starting asteroid.
			var selectable = pod.GetComponent<KSelectable>();
			return selectable != null ? selectable.GetMyWorldId() : 0;
		}
	}
}
