namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Decides whether a player is allowed to do something to an owned object.
	/// </summary>
	/// <remarks>
	/// Declared now so the ownership model and the rules that will consume it are designed together,
	/// but deliberately left unimplemented: enforcement lands with the phases that need it, once there
	/// is something to own. Wiring an implementation in before printing pods exist would mean writing
	/// rules with nothing to test them against.
	/// <para>
	/// Every method answers for the host. Client side checks only exist to grey out UI; a client that
	/// lies is caught here, which is why enforcement has to sit behind packet dispatch rather than in
	/// the tool that sends the packet.
	/// </para>
	/// <para>
	/// The split between <see cref="CanConfigureBuilding"/> and <see cref="CanOperateBuilding"/> is the
	/// core rule of this mod: ordering work is shared, running a building is not. Digging and building
	/// come from one pool that anyone's duplicants may pick up, while a research station only runs for
	/// the duplicants of the player who owns it.
	/// </para>
	/// </remarks>
	public interface IPermissionService
	{
		/// <summary>
		/// Direct commands to a duplicant: move here, set skills, priorities, schedule, diet, rename.
		/// Owner only.
		/// </summary>
		bool CanDirectControl(PlayerId actor, int duplicantNetId);

		/// <summary>Changing a building's settings or ordering it deconstructed. Owner only.</summary>
		bool CanConfigureBuilding(PlayerId actor, int buildingNetId);

		/// <summary>
		/// Whether a duplicant may work at a building - research, cook, generate power, exercise.
		/// Judged on the duplicant's owner against the building's owner, not on the acting player,
		/// because the duplicant picks this work up on its own.
		/// </summary>
		bool CanOperateBuilding(int duplicantNetId, int buildingNetId);

		/// <summary>
		/// Whether the player may place a build order for this building type. This is where separated
		/// research is enforced: unlocks are per player, and a player cannot have a teammate order
		/// something they have not researched themselves.
		/// </summary>
		bool CanOrderBuild(PlayerId actor, string buildingDefId);

		/// <summary>Whether the player may print a duplicant from this pod. Owner only.</summary>
		bool CanPrint(PlayerId actor, int printingPodNetId);
	}
}
