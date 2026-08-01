using System;
using ONI_Together.Misc;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Identifies a player across the session and across saves.
	/// </summary>
	/// <remarks>
	/// Wraps the same ulong the rest of the mod already uses for players - a Steam id, or a Riptide
	/// connection id on LAN. The wrapper exists so ownership APIs cannot silently accept a NetId,
	/// a world id or any other loose ulong that happens to be in scope.
	/// </remarks>
	public readonly struct PlayerId : IEquatable<PlayerId>
	{
		/// <summary>
		/// Absence of a player. Matches <see cref="Utils.NilUlong"/> so it lines up with the rest of the codebase.
		/// </summary>
		public static readonly PlayerId None = new PlayerId(0uL);

		public readonly ulong Value;

		public PlayerId(ulong value)
		{
			Value = value;
		}

		public bool IsValid => Value != 0uL;

		/// <summary>
		/// The player at this client. Only meaningful inside a session.
		/// </summary>
		public static PlayerId Local => new PlayerId(MultiplayerSession.LocalUserID);

		/// <summary>
		/// The authoritative player. Only meaningful inside a session.
		/// </summary>
		public static PlayerId Host => new PlayerId(MultiplayerSession.HostUserID);

		/// <summary>
		/// Player the packet currently being dispatched came from, or <see cref="None"/> if unattributed.
		/// </summary>
		public static PlayerId CurrentSender => new PlayerId(PacketContext.CurrentSender);

		public bool Equals(PlayerId other) => Value == other.Value;

		public override bool Equals(object obj) => obj is PlayerId other && Equals(other);

		public override int GetHashCode() => Value.GetHashCode();

		public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);

		public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);

		public override string ToString()
		{
			if (!IsValid)
				return "PlayerId(none)";

			return MultiplayerSession.KnownPlayerNames.TryGetValue(Value, out var name)
				? $"{name}({Value})"
				: $"PlayerId({Value})";
		}
	}
}
