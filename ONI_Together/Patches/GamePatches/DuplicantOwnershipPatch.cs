using System;
using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Misc;
using ONI_Together.Networking;
using ONI_Together.Networking.Components;
using ONI_Together.Networking.Ownership;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Patches.GamePatches
{
	/// <summary>
	/// Makes a printed duplicant belong to whoever owns the pod that printed it.
	/// </summary>
	/// <remarks>
	/// The pod and the duplicant are known in different places, so the pod is scoped around the
	/// delivery - see <see cref="PrintingPodContext"/> - and read back when the duplicant appears.
	/// </remarks>
	public static class DuplicantOwnershipPatches
	{
		/// <summary>
		/// Puts the component on the duplicant prefab so saved ownership has somewhere to load into.
		/// </summary>
		/// <remarks>
		/// Attaching it later does not work: loading applies saved values only to components the
		/// instance already has, which is what made pod ownership vanish across a reload before it was
		/// moved to prefab time.
		/// <para>
		/// Declared through SaveLoadRoot as well, matching how the mod already registers
		/// NetworkIdentity on duplicants.
		/// </para>
		/// </remarks>
		[HarmonyPatch(typeof(BaseMinionConfig), nameof(BaseMinionConfig.BaseMinion))]
		public static class MinionPrefabPatch
		{
			public static void Postfix(GameObject __result)
			{
				using var _ = Profiler.Scope();

				if (__result == null) return;

				var saveRoot = __result.GetComponent<SaveLoadRoot>();
				if (saveRoot != null)
					saveRoot.TryDeclareOptionalComponent<OwnershipComponent>();

				__result.AddOrGet<OwnershipComponent>();
			}
		}

		/// <summary>
		/// Marks which pod is delivering, so the duplicant that comes out can be traced back to it.
		/// </summary>
		[HarmonyPatch(typeof(Telepad), nameof(Telepad.OnAcceptDelivery))]
		public static class TelepadDeliveryScopePatch
		{
			[ThreadStatic]
			private static PrintingPodContext.PodScope _scope;

			public static void Prefix(Telepad __instance)
			{
				_scope = PrintingPodContext.Scope(__instance);
			}

			public static void Postfix()
			{
				// Paired with Prefix. If Deliver throws, the scope outlives the call - acceptable,
				// because the next delivery overwrites it and nothing reads it in between.
				_scope.Dispose();
			}
		}

		/// <summary>
		/// Assigns the new duplicant to the pod's owner.
		/// </summary>
		/// <remarks>
		/// Host only. Ownership decided on a client would be a claim, not a fact.
		/// <para>
		/// Unlike pods, this does not reach clients on its own. A pod arrives inside the save the
		/// client is sent, so its ownership comes along for free; a duplicant printed mid-session does
		/// not exist in any save the client has. Clients will therefore be right about it only after
		/// the next hard sync until a packet carries it, which is the next piece of work.
		/// </para>
		/// </remarks>
		[HarmonyPatch(typeof(MinionStartingStats), nameof(MinionStartingStats.Deliver))]
		public static class MinionDeliverOwnershipPatch
		{
			public static void Postfix(GameObject __result)
			{
				using var _ = Profiler.Scope();

				if (!MultiplayerSession.InSession) return;
				if (!MultiplayerSession.IsHost) return;
				if (__result == null) return;

				try
				{
					var pod = PrintingPodContext.Current;
					if (pod == null)
					{
						// Duplicants also arrive by means other than printing, such as the starting
						// crew during worldgen. Those have no pod to inherit from and stay unowned.
						return;
					}

					if (!pod.TryGetComponent<OwnershipComponent>(out var podOwnership) || !podOwnership.HasOwner)
					{
						DebugConsole.LogWarning("[DuplicantOwnership] Pod has no owner; the duplicant it printed is unowned.");
						return;
					}

					var identity = __result.AddOrGet<NetworkIdentity>();
					if (identity.NetId == 0)
						identity.RegisterIdentity();

					if (identity.NetId == 0)
					{
						DebugConsole.LogWarning("[DuplicantOwnership] Printed duplicant has no usable NetId; leaving it unowned.");
						return;
					}

					var ownership = __result.AddOrGet<OwnershipComponent>();
					ownership.Assign(podOwnership.Owner, OwnershipType.Duplicant, podOwnership.WorldId);
				}
				catch (System.Exception ex)
				{
					// An unowned duplicant is a bug worth seeing; one that throws here would abort the
					// print and leave the pod in a broken state.
					DebugConsole.LogWarning($"[DuplicantOwnership] Failed to inherit pod ownership: {ex}");
				}
			}
		}
	}
}
