using System.Collections.Generic;
using ONI_Together.DebugTools;

namespace ONI_Together.Networking
{
	/// <summary>
	/// Reports who inbound packets are being attributed to.
	/// </summary>
	/// <remarks>
	/// <see cref="PacketContext"/> can be wrong in a way nothing notices until permission checks are
	/// built on top of it and start denying the wrong player. This makes attribution observable before
	/// anything depends on it.
	/// <para>
	/// Packets arrive constantly, so logging each one would bury the log. Only transitions are
	/// reported: the first packet from a given player, and unattributed packets at a decreasing rate.
	/// A session with two players should therefore produce exactly one "first packet" line per player,
	/// and no unattributed warnings at all.
	/// </para>
	/// </remarks>
	public static class PacketSenderDiagnostics
	{
		private static readonly HashSet<ulong> _seenSenders = new HashSet<ulong>();
		private static int _unattributedCount;

		public static void Observe(ulong senderId, string packetTypeName)
		{
			if (senderId == PacketContext.Unknown)
			{
				_unattributedCount++;

				// Back off quickly: if attribution is broken it is broken for everything, and one line
				// per packet would be thousands of lines a minute.
				if (_unattributedCount <= 3 || _unattributedCount % 1000 == 0)
				{
					DebugConsole.LogWarning(
						$"[PacketSender] Unattributed packet #{_unattributedCount} ({packetTypeName}). " +
						"Permission checks will treat this as untrusted.");
				}

				return;
			}

			if (_seenSenders.Add(senderId))
			{
				string who = MultiplayerSession.KnownPlayerNames.TryGetValue(senderId, out var name) ? name : "unknown name";
				DebugConsole.Log(
					$"[PacketSender] First packet attributed to {senderId} ({who}) via {packetTypeName}. " +
					$"local={MultiplayerSession.LocalUserID} host={MultiplayerSession.HostUserID} isHost={MultiplayerSession.IsHost}");
			}
		}

		/// <summary>Reset alongside the session, so a new session reports its senders afresh.</summary>
		public static void Clear()
		{
			_seenSenders.Clear();
			_unattributedCount = 0;
		}
	}
}
