using System.Text;
using ONI_Together.DebugTools;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Structured logging for ownership and permission events.
	/// </summary>
	/// <remarks>
	/// Emits flat key=value lines so a Player.log from a play session can be grepped and diffed between
	/// host and client. Free text would make that impossible once several players are issuing commands.
	/// <para>
	/// Only fields that actually exist today are emitted. Tick and session id are named in the plan's
	/// log schema but the mod has no session id yet, so they are left out rather than logged as blanks.
	/// </para>
	/// </remarks>
	public static class OwnershipLog
	{
		private const string Prefix = "[Ownership]";

		/// <summary>Records a change to the ownership index.</summary>
		public static void Event(string action, int netId, PlayerId owner, OwnershipType type, int worldId, string result)
		{
			DebugConsole.Log(Build(action, netId, owner, type, worldId, result, actor: PlayerId.CurrentSender));
		}

		/// <summary>
		/// Records a rejected operation. Warning rather than log because a rejection during normal play
		/// means either a bug or a client sending something it should not.
		/// </summary>
		public static void Rejected(string action, int netId, PlayerId owner, OwnershipType type, int worldId, string reason)
		{
			DebugConsole.LogWarning(Build(action, netId, owner, type, worldId, "rejected", actor: PlayerId.CurrentSender, reason: reason));
		}

		/// <summary>
		/// Records a command the host refused because the sender does not own the target.
		/// </summary>
		/// <remarks>
		/// Warning level on purpose. In normal play a client greys out what it cannot do, so a denial
		/// reaching the host means either the UI and the rules disagree or something bypassed the UI.
		/// Both are worth seeing.
		/// </remarks>
		public static void Denied(string command, PlayerId actor, int netId, PlayerId owner)
		{
			DebugConsole.LogWarning(
				$"{Prefix} action=denied command={command} netId={netId} " +
				$"actor={actor.Value} owner={owner.Value}");
		}

		private static string Build(
			string action,
			int netId,
			PlayerId owner,
			OwnershipType type,
			int worldId,
			string result,
			PlayerId actor,
			string reason = null)
		{
			var sb = new StringBuilder(Prefix.Length + 96);
			sb.Append(Prefix)
			  .Append(" action=").Append(action)
			  .Append(" netId=").Append(netId)
			  .Append(" owner=").Append(owner.Value)
			  .Append(" type=").Append(type)
			  .Append(" world=").Append(worldId)
			  .Append(" result=").Append(result);

			// Only present when this happened while handling an inbound packet.
			if (actor.IsValid)
				sb.Append(" actor=").Append(actor.Value);

			if (!string.IsNullOrEmpty(reason))
				sb.Append(" reason=").Append(reason);

			return sb.ToString();
		}
	}
}
