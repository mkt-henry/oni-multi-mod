using HarmonyLib;
using ONI_Together.Networking;
using ONI_Together.Networking.Ownership;
using ONI_Together.Networking.Packets.DuplicantActions;
using ONI_Together.Networking.Packets.Tools.Move;
using Shared.Profiling;

namespace ONI_Together.Patches.Permissions
{
	/// <summary>
	/// Stops a player issuing direct orders to someone else's duplicant.
	/// </summary>
	/// <remarks>
	/// This is the rule the mod exists for. Ordering work stays shared - anyone's duplicants may pick
	/// up any dig or build - but telling a specific duplicant what to do is the owner's alone.
	/// <para>
	/// Enforced by refusing the packet on the host rather than by hiding the button, because a client
	/// is free to send whatever it likes. Written as prefixes on the packets instead of edits inside
	/// them so the rules live in one place and upstream files stay untouched.
	/// </para>
	/// <para>
	/// The actor comes from <see cref="PacketContext"/>, which is why that had to be correct first.
	/// A packet that arrives unattributed is refused: a command nobody can be held to is exactly what
	/// the ownership rules are meant to prevent.
	/// </para>
	/// </remarks>
	public static class DuplicantControlPermissionPatches
	{
		/// <summary>
		/// Shared gate. Returns true when the command should be allowed to run.
		/// </summary>
		private static bool Allow(string command, int duplicantNetId)
		{
			using var _ = Profiler.Scope();

			// Only the host decides. On a client these packets are already no-ops.
			if (!MultiplayerSession.IsHostInSession)
				return true;

			var actor = PlayerId.CurrentSender;

			// Locally issued commands do not arrive as packets, so there is no sender to check. This
			// path is reached when the host itself acts, which needs no permission.
			if (!actor.IsValid)
				return true;

			if (PermissionService.Instance.CanDirectControl(actor, duplicantNetId))
				return true;

			OwnershipRegistry.Instance.TryGetOwner(duplicantNetId, out var owner);
			OwnershipLog.Denied(command, actor, duplicantNetId, owner);
			return false;
		}

		[HarmonyPatch(typeof(MoveToLocationPacket), nameof(MoveToLocationPacket.OnDispatched))]
		public static class MovePermission
		{
			public static bool Prefix(MoveToLocationPacket __instance)
				=> Allow("move", __instance.TargetNetId);
		}

		[HarmonyPatch(typeof(SkillMasteryPacket), nameof(SkillMasteryPacket.OnDispatched))]
		public static class SkillPermission
		{
			public static bool Prefix(SkillMasteryPacket __instance)
				=> Allow("skill", __instance.NetId);
		}

		[HarmonyPatch(typeof(DuplicantPriorityPacket), nameof(DuplicantPriorityPacket.OnDispatched))]
		public static class PriorityPermission
		{
			public static bool Prefix(DuplicantPriorityPacket __instance)
				=> Allow("priority", __instance.NetId);
		}
	}
}
