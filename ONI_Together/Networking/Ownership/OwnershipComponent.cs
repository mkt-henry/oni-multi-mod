using KSerialization;
using ONI_Together.DebugTools;
using ONI_Together.Networking.Components;
using Shared.Profiling;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Persists which player owns the object this is attached to, and keeps
	/// <see cref="OwnershipRegistry.Instance"/> in step with it.
	/// </summary>
	/// <remarks>
	/// The component, not the registry, is the source of truth. Writing ownership into the save through
	/// KSerialization means it survives save/load, rejoin and hard sync without any separate metadata
	/// file to keep in agreement - a hard sync ships the host's save, so the client rebuilds the same
	/// ownership from the same bytes.
	/// <para>
	/// Requires a <see cref="NetworkIdentity"/> on the same object, since ownership is keyed by NetId.
	/// </para>
	/// </remarks>
	[SerializationConfig(MemberSerialization.OptIn)]
	public class OwnershipComponent : KMonoBehaviour
	{
		[Serialize]
		public ulong OwnerId;

		/// <summary>
		/// Stored as the underlying byte rather than the enum so a future rename of a member cannot
		/// change what old saves deserialise to.
		/// </summary>
		[Serialize]
		public byte OwnedTypeRaw;

		[Serialize]
		public int WorldId;

		public PlayerId Owner => new PlayerId(OwnerId);

		public OwnershipType OwnedType => (OwnershipType)OwnedTypeRaw;

		public bool HasOwner => Owner.IsValid && OwnedType != OwnershipType.None;

		public override void OnSpawn()
		{
			using var _ = Profiler.Scope();

			base.OnSpawn();

			// Nothing to restore on a freshly placed object; Assign will register it when ownership is set.
			if (!HasOwner)
				return;

			PublishToRegistry("spawn");
		}

		/// <summary>
		/// Sets the owner and records it. This is the only way ownership should be established.
		/// </summary>
		/// <remarks>
		/// Callers are responsible for having decided that the change is legitimate - this does not
		/// perform a permission check, it is the thing permission checks will later guard.
		/// </remarks>
		public bool Assign(PlayerId owner, OwnershipType type, int worldId)
		{
			using var _ = Profiler.Scope();

			OwnerId = owner.Value;
			OwnedTypeRaw = (byte)type;
			WorldId = worldId;

			return PublishToRegistry("assign");
		}

		/// <summary>Clears ownership, leaving the object unowned.</summary>
		public void Revoke()
		{
			using var _ = Profiler.Scope();

			if (TryGetNetId(out int netId))
				OwnershipRegistry.Instance.Remove(netId);

			OwnerId = PlayerId.None.Value;
			OwnedTypeRaw = (byte)OwnershipType.None;
		}

		public override void OnCleanUp()
		{
			using var _ = Profiler.Scope();

			// Drop the index entry but leave the serialised fields alone: the object may be being
			// unloaded rather than destroyed, and it has to come back owned.
			if (TryGetNetId(out int netId))
				OwnershipRegistry.Instance.Remove(netId);

			base.OnCleanUp();
		}

		private bool PublishToRegistry(string origin)
		{
			if (!TryGetNetId(out int netId))
			{
				DebugConsole.LogWarning($"[Ownership] {gameObject.name} has ownership but no usable NetId ({origin}); it will not be tracked.");
				return false;
			}

			return OwnershipRegistry.Instance.Register(netId, Owner, OwnedType, WorldId);
		}

		private bool TryGetNetId(out int netId)
		{
			netId = 0;

			if (!TryGetComponent<NetworkIdentity>(out var identity))
				return false;

			// Component OnSpawn order is not guaranteed, so NetworkIdentity may not have assigned its
			// NetId yet. RegisterIdentity is guarded against running twice, so asking early is safe.
			if (identity.NetId == 0)
				identity.RegisterIdentity();

			netId = identity.NetId;
			return netId != 0;
		}
	}
}
