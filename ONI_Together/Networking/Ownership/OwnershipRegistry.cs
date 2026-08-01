using System.Collections.Generic;
using System.Linq;
using Shared.Profiling;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Default <see cref="IOwnershipRegistry"/>: a NetId keyed table plus a reverse index by owner.
	/// </summary>
	/// <remarks>
	/// The reverse index exists because the queries this has to answer during play are almost all
	/// "what does this player own" - their duplicants, their buildings, what to hand over when they
	/// disconnect. Scanning every record for those would be O(n) on a table that grows with the colony.
	/// The two structures are only ever written together, in this class.
	/// </remarks>
	public sealed class OwnershipRegistry : IOwnershipRegistry
	{
		/// <summary>
		/// Registry for the running session. Tests construct their own instance instead of touching this.
		/// </summary>
		public static IOwnershipRegistry Instance { get; } = new OwnershipRegistry();

		private readonly Dictionary<int, OwnershipRecord> _records = new Dictionary<int, OwnershipRecord>();
		private readonly Dictionary<ulong, HashSet<int>> _byOwner = new Dictionary<ulong, HashSet<int>>();

		public int Count => _records.Count;

		public IEnumerable<OwnershipRecord> All => _records.Values;

		public bool Register(int netId, PlayerId owner, OwnershipType type, int worldId)
		{
			using var _ = Profiler.Scope();

			// NetId 0 means NetworkIdentity has not assigned one yet. Recording it would create an entry
			// that can never be matched back to an object.
			if (netId == 0)
			{
				OwnershipLog.Rejected("register", netId, owner, type, worldId, "netId-unassigned");
				return false;
			}

			if (!owner.IsValid)
			{
				OwnershipLog.Rejected("register", netId, owner, type, worldId, "owner-invalid");
				return false;
			}

			if (type == OwnershipType.None)
			{
				OwnershipLog.Rejected("register", netId, owner, type, worldId, "type-none");
				return false;
			}

			if (_records.TryGetValue(netId, out var existing))
			{
				// Idempotent re-registration is expected: OnSpawn can run again after a reload.
				if (existing.Owner == owner && existing.Type == type && existing.WorldId == worldId)
					return true;

				// Anything else is a genuine change. The host is authoritative, so overwrite rather than
				// refuse - but say so, because two different owners for one object means a bug upstream.
				DetachFromOwner(existing);
				_records[netId] = new OwnershipRecord(netId, owner, type, worldId);
				AttachToOwner(_records[netId]);
				OwnershipLog.Event("register", netId, owner, type, worldId, $"replaced-owner-{existing.Owner.Value}");
				return true;
			}

			var record = new OwnershipRecord(netId, owner, type, worldId);
			_records[netId] = record;
			AttachToOwner(record);
			OwnershipLog.Event("register", netId, owner, type, worldId, "ok");
			return true;
		}

		public bool Transfer(int netId, PlayerId newOwner)
		{
			using var _ = Profiler.Scope();

			if (!_records.TryGetValue(netId, out var existing))
			{
				OwnershipLog.Rejected("transfer", netId, newOwner, OwnershipType.None, 0, "not-registered");
				return false;
			}

			if (!newOwner.IsValid)
			{
				OwnershipLog.Rejected("transfer", netId, newOwner, existing.Type, existing.WorldId, "owner-invalid");
				return false;
			}

			if (existing.Owner == newOwner)
				return true;

			DetachFromOwner(existing);
			var updated = existing.WithOwner(newOwner);
			_records[netId] = updated;
			AttachToOwner(updated);

			OwnershipLog.Event("transfer", netId, newOwner, updated.Type, updated.WorldId, $"from-{existing.Owner.Value}");
			return true;
		}

		public bool Remove(int netId)
		{
			using var _ = Profiler.Scope();

			if (!_records.TryGetValue(netId, out var existing))
				return false;

			DetachFromOwner(existing);
			_records.Remove(netId);

			OwnershipLog.Event("remove", netId, existing.Owner, existing.Type, existing.WorldId, "ok");
			return true;
		}

		public bool TryGetOwner(int netId, out PlayerId owner)
		{
			if (_records.TryGetValue(netId, out var record))
			{
				owner = record.Owner;
				return true;
			}

			owner = PlayerId.None;
			return false;
		}

		public bool TryGetRecord(int netId, out OwnershipRecord record) => _records.TryGetValue(netId, out record);

		public bool IsOwnedBy(int netId, PlayerId player)
		{
			// Unowned answers false so that callers gating on this deny rather than allow.
			return player.IsValid && _records.TryGetValue(netId, out var record) && record.Owner == player;
		}

		public bool IsOwned(int netId) => _records.ContainsKey(netId);

		public IEnumerable<OwnershipRecord> ByOwner(PlayerId owner)
		{
			if (!owner.IsValid || !_byOwner.TryGetValue(owner.Value, out var netIds))
				return Enumerable.Empty<OwnershipRecord>();

			// Materialised so callers can remove while iterating - cleaning up a departed player's
			// objects is a normal thing to want to do with this result.
			var result = new List<OwnershipRecord>(netIds.Count);
			foreach (int netId in netIds)
			{
				if (_records.TryGetValue(netId, out var record))
					result.Add(record);
			}

			return result;
		}

		public void Clear()
		{
			using var _ = Profiler.Scope();

			_records.Clear();
			_byOwner.Clear();
		}

		private void AttachToOwner(OwnershipRecord record)
		{
			if (!_byOwner.TryGetValue(record.Owner.Value, out var netIds))
			{
				netIds = new HashSet<int>();
				_byOwner[record.Owner.Value] = netIds;
			}

			netIds.Add(record.NetId);
		}

		private void DetachFromOwner(OwnershipRecord record)
		{
			if (!_byOwner.TryGetValue(record.Owner.Value, out var netIds))
				return;

			netIds.Remove(record.NetId);

			// Drop empty buckets so a long session does not accumulate one per player that ever owned anything.
			if (netIds.Count == 0)
				_byOwner.Remove(record.Owner.Value);
		}
	}
}
