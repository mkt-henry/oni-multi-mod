using HarmonyLib;
using ONI_Together.Networking.Ownership;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Patches.GamePatches
{
	/// <summary>
	/// Puts <see cref="OwnershipComponent"/> on the printing pod prefab so that saved ownership can be
	/// read back.
	/// </summary>
	/// <remarks>
	/// This exists because attaching the component at runtime did not survive a reload, which was
	/// measured rather than assumed: after a hosted save and reload the pod came back unowned and was
	/// reassigned from scratch.
	/// <para>
	/// The reason is the order KSerialization works in. Loading instantiates the building's prefab and
	/// then applies saved values to the components that instance already has. A component added later,
	/// during Telepad.OnSpawn, is not there when the data is applied, so the saved values have nothing
	/// to land on and are discarded. Writing them worked fine - the type and its three fields are
	/// present in the save file - but reading them back needs the component to exist beforehand.
	/// </para>
	/// <para>
	/// NetworkIdentity does not need this treatment. Its NetId is recomputed deterministically from the
	/// object, so it comes back correct whether or not it was persisted. Ownership cannot be derived
	/// from anything, so it has to be stored and actually restored.
	/// </para>
	/// <para>
	/// Consequence worth being explicit about: prefabs are configured once at startup, long before
	/// anyone chooses to host, so this cannot be limited to multiplayer. Every printing pod now carries
	/// the component in singleplayer too, and three default-valued fields are written into singleplayer
	/// saves. No behaviour changes - an unowned component registers nothing and gates nothing - but it
	/// is a departure from leaving singleplayer saves byte-identical, and a save written with the mod
	/// will mention a type that vanilla does not know.
	/// </para>
	/// </remarks>
	[HarmonyPatch(typeof(HeadquartersConfig), nameof(HeadquartersConfig.DoPostConfigureComplete))]
	public static class HeadquartersOwnershipPrefabPatch
	{
		public static void Postfix(GameObject go)
		{
			using var _ = Profiler.Scope();

			if (go == null) return;

			// Present from instantiation onwards, which is the whole point; it stays unowned until
			// something assigns it.
			go.AddOrGet<OwnershipComponent>();
		}
	}
}
