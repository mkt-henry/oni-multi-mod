namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Answers <see cref="IPermissionService"/> from the ownership registry.
	/// </summary>
	/// <remarks>
	/// Only meaningful on the host. A client asking these questions is guessing, usefully - to grey
	/// out a button - but the answer that counts is the one computed here, after a packet arrives,
	/// where a client that lies about who it is cannot reach.
	/// </remarks>
	public sealed class PermissionService : IPermissionService
	{
		public static IPermissionService Instance { get; } = new PermissionService(OwnershipRegistry.Instance);

		private readonly IOwnershipRegistry _ownership;

		public PermissionService(IOwnershipRegistry ownership)
		{
			_ownership = ownership;
		}

		/// <summary>
		/// Whether the actor may act on this object.
		/// </summary>
		/// <remarks>
		/// Unowned objects are open to everyone, which is a deliberate policy and not the same thing
		/// as the registry's fail-closed lookups. The registry refuses to claim someone owns what it
		/// has no record of; this refuses to protect what nobody has claimed. Doing otherwise would
		/// freeze the starting crew, who are placed by worldgen with no pod to inherit from and stay
		/// unowned until starting areas are assigned per player.
		/// </remarks>
		private bool MayAct(PlayerId actor, int netId)
		{
			if (!_ownership.TryGetRecord(netId, out var record))
				return true;

			return record.Owner == actor;
		}

		public bool CanDirectControl(PlayerId actor, int duplicantNetId) => MayAct(actor, duplicantNetId);

		public bool CanConfigureBuilding(PlayerId actor, int buildingNetId) => MayAct(actor, buildingNetId);

		public bool CanPrint(PlayerId actor, int printingPodNetId) => MayAct(actor, printingPodNetId);

		/// <summary>
		/// Whether a duplicant may work at a building.
		/// </summary>
		/// <remarks>
		/// Judged owner against owner, because the duplicant takes this work on its own rather than
		/// being told to. An unowned building is public, and an unowned duplicant may work anywhere.
		/// </remarks>
		public bool CanOperateBuilding(int duplicantNetId, int buildingNetId)
		{
			if (!_ownership.TryGetRecord(buildingNetId, out var building))
				return true;

			if (!_ownership.TryGetOwner(duplicantNetId, out var duplicantOwner))
				return true;

			return building.Owner == duplicantOwner;
		}

		/// <summary>
		/// Whether the player may order this building type.
		/// </summary>
		/// <remarks>
		/// Always true for now, and deliberately so rather than as a placeholder: this gate exists to
		/// stop a player ordering something they have not researched, and research is still shared.
		/// There is nothing to enforce until it is separated, and refusing on a guess would break
		/// building for everyone.
		/// </remarks>
		public bool CanOrderBuild(PlayerId actor, string buildingDefId) => true;
	}
}
