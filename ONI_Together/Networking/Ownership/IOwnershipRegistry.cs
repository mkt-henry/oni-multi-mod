using System.Collections.Generic;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Index of which player owns which networked object.
	/// </summary>
	/// <remarks>
	/// This is an in-memory index, not the source of truth. Ownership is persisted per object by
	/// <see cref="OwnershipComponent"/> so it rides along in the save file, which also means it
	/// survives a hard sync for free: the client receives the host's save and rebuilds the index
	/// from it. The registry is repopulated as objects spawn.
	/// <para>
	/// Interface rather than a static class so that tests can exercise a throwaway instance instead
	/// of mutating live session state.
	/// </para>
	/// </remarks>
	public interface IOwnershipRegistry
	{
		int Count { get; }

		IEnumerable<OwnershipRecord> All { get; }

		/// <summary>
		/// Records that <paramref name="owner"/> owns the object with the given NetId.
		/// Re-registering an already-owned object replaces the record; the host is authoritative and
		/// is allowed to correct itself.
		/// </summary>
		/// <param name="origin">
		/// What caused this, used as the action in the log. Distinguishing a restore from a fresh
		/// assignment matters when reading a session log: they mean very different things, and a
		/// restore appearing where an assignment belongs is how a persistence bug shows itself.
		/// </param>
		/// <returns>False when the arguments are unusable, in which case nothing is recorded.</returns>
		bool Register(int netId, PlayerId owner, OwnershipType type, int worldId, string origin = "register");

		/// <summary>Hands an existing object to a different player, keeping its type and world.</summary>
		/// <returns>False when the object has no record.</returns>
		bool Transfer(int netId, PlayerId newOwner);

		/// <returns>False when there was nothing to remove.</returns>
		bool Remove(int netId);

		bool TryGetOwner(int netId, out PlayerId owner);

		bool TryGetRecord(int netId, out OwnershipRecord record);

		/// <summary>
		/// Whether the object is owned by this player. Unowned objects answer false, so callers that
		/// gate on this fail closed rather than open.
		/// </summary>
		bool IsOwnedBy(int netId, PlayerId player);

		bool IsOwned(int netId);

		IEnumerable<OwnershipRecord> ByOwner(PlayerId owner);

		/// <summary>Drops everything. Called when a session ends or a hard sync replaces the world.</summary>
		void Clear();
	}
}
