using System;
using ONI_Together.Misc;

namespace ONI_Together.Networking
{
	/// <summary>
	/// Ambient identity of the player whose packet is currently being dispatched.
	/// <para>
	/// The transport layer knows who a packet came from, but <see cref="Packets.Architecture.IPacket.OnDispatched"/>
	/// takes no arguments, so that information used to be dropped at the receive site. Anything that needs to
	/// attribute a packet to a player - ownership checks, permission checks, audit logging - reads it from here
	/// instead.
	/// </para>
	/// <para>
	/// This is deliberately an ambient scope rather than a parameter on <c>IPacket.OnDispatched</c>:
	/// that interface is part of the public mod API surface, and changing its signature would break
	/// third party packets and create a permanent merge conflict with upstream.
	/// </para>
	/// <para>
	/// Nested dispatches (<c>BulkSenderPacket</c>, <c>DedicatedServerMessagePacket</c>) inherit the enclosing
	/// scope automatically because they unpack inside the same receive call.
	/// </para>
	/// </summary>
	/// <remarks>
	/// On a client the sender is always the host, because clients only ever receive host traffic.
	/// Attribution is only authoritative on the host, which is where permission checks belong.
	/// </remarks>
	public static class PacketContext
	{
		[ThreadStatic]
		private static ulong _currentSender;

		/// <summary>
		/// Sentinel used when the sender could not be determined.
		/// </summary>
		public static ulong Unknown => Utils.NilUlong();

		/// <summary>
		/// Player id the packet being dispatched came from, or <see cref="Unknown"/> outside of a dispatch.
		/// </summary>
		public static ulong CurrentSender => _currentSender;

		/// <summary>
		/// False outside of a dispatch, or when the transport could not attribute the packet.
		/// Callers that gate behaviour on identity must treat this as "deny" rather than "allow".
		/// </summary>
		public static bool HasSender => _currentSender != Utils.NilUlong();

		/// <summary>
		/// True when the packet being dispatched came from the local player.
		/// </summary>
		public static bool IsFromLocalPlayer => HasSender && _currentSender == MultiplayerSession.LocalUserID;

		/// <summary>
		/// Sets the sender for the duration of the returned scope and restores the previous value on dispose.
		/// Restoring rather than clearing is what makes nested dispatch safe.
		/// </summary>
		public static SenderScope Scope(ulong senderId)
		{
			return new SenderScope(senderId);
		}

		/// <summary>
		/// Struct so that scoping a dispatch costs no allocation - this runs on every inbound packet.
		/// </summary>
		public readonly struct SenderScope : IDisposable
		{
			private readonly ulong _previous;

			internal SenderScope(ulong senderId)
			{
				_previous = _currentSender;
				_currentSender = senderId;
			}

			public void Dispose()
			{
				_currentSender = _previous;
			}
		}
	}
}
