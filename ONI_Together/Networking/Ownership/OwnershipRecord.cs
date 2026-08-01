namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Says that one networked object belongs to one player.
	/// </summary>
	/// <remarks>
	/// Keyed by <see cref="NetId"/> rather than a new identifier type: the mod already generates a
	/// deterministic NetId per object and serialises it into the save, which is exactly what an
	/// ownership key has to be. Adding a parallel id would mean keeping two of them in agreement.
	/// </remarks>
	public readonly struct OwnershipRecord
	{
		/// <summary>Identity of the owned object, from its NetworkIdentity.</summary>
		public readonly int NetId;

		public readonly PlayerId Owner;

		public readonly OwnershipType Type;

		/// <summary>
		/// Asteroid the object was on when ownership was recorded.
		/// </summary>
		/// <remarks>
		/// Always 0 while the MVP is single-asteroid, but carried from the start because retrofitting a
		/// world dimension onto ownership later would touch every call site. Objects that can change
		/// world - rockets and their contents - will need this refreshed when multi-asteroid support lands.
		/// </remarks>
		public readonly int WorldId;

		public OwnershipRecord(int netId, PlayerId owner, OwnershipType type, int worldId)
		{
			NetId = netId;
			Owner = owner;
			Type = type;
			WorldId = worldId;
		}

		public OwnershipRecord WithOwner(PlayerId newOwner) => new OwnershipRecord(NetId, newOwner, Type, WorldId);

		public override string ToString() => $"netId={NetId} owner={Owner} type={Type} world={WorldId}";
	}
}
