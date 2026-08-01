using System.Collections.Generic;
using System.IO;
using ONI_Together.DebugTools;
using ONI_Together.Networking.Ownership;
using ONI_Together.Networking.Packets.Architecture;
using Shared.Profiling;

namespace ONI_Together.Networking.Packets.World
{
	/// <summary>
	/// Tells clients who owns an object whose ownership was decided while the session was running.
	/// </summary>
	/// <remarks>
	/// Most ownership never needs this. A printing pod exists in the world the client is sent, so its
	/// owner arrives inside the save file and no packet is involved. A duplicant printed mid session
	/// is in no save the client holds, so without this it would stay unowned there until the next hard
	/// sync - and permission checks on the client would disagree with the host.
	/// <para>
	/// Ownership is still decided solely by the host; this only reports the decision.
	/// </para>
	/// </remarks>
	public class OwnershipSyncPacket : IPacket
	{
		/// <summary>
		/// Ownership that arrived before the object it describes.
		/// </summary>
		/// <remarks>
		/// The entity spawn packet and this one are produced by two separate postfixes on the same
		/// method, and Harmony does not order those, so this can land first. Dropping it in that case
		/// would leave a permanently unowned duplicant, so it is held until the object shows up and
		/// <see cref="OwnershipComponent"/> claims it on spawn.
		/// </remarks>
		private static readonly Dictionary<int, OwnershipSyncPacket> _pending = new Dictionary<int, OwnershipSyncPacket>();

		public int NetId;
		public ulong OwnerId;
		public byte OwnedType;
		public int WorldId;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(NetId);
			writer.Write(OwnerId);
			writer.Write(OwnedType);
			writer.Write(WorldId);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			NetId = reader.ReadInt32();
			OwnerId = reader.ReadUInt64();
			OwnedType = reader.ReadByte();
			WorldId = reader.ReadInt32();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			// The host is where this came from; applying it there would be a loop.
			if (MultiplayerSession.IsHost) return;

			if (NetworkIdentityRegistry.TryGet(NetId, out var identity) && identity != null)
			{
				identity.gameObject.AddOrGet<OwnershipComponent>()
					.ApplyRemote(new PlayerId(OwnerId), (OwnershipType)OwnedType, WorldId);
				return;
			}

			_pending[NetId] = this;
		}

		/// <summary>
		/// Hands over ownership that arrived early, if any is waiting for this object.
		/// </summary>
		public static bool TryTakePending(int netId, out PlayerId owner, out OwnershipType type, out int worldId)
		{
			owner = PlayerId.None;
			type = OwnershipType.None;
			worldId = 0;

			if (!_pending.TryGetValue(netId, out var packet))
				return false;

			_pending.Remove(netId);
			owner = new PlayerId(packet.OwnerId);
			type = (OwnershipType)packet.OwnedType;
			worldId = packet.WorldId;
			return true;
		}

		public static void ClearPending()
		{
			int abandoned = _pending.Count;
			_pending.Clear();

			// Anything still queued describes an object that never arrived, which is worth knowing.
			if (abandoned > 0)
				DebugConsole.LogWarning($"[Ownership] Dropped {abandoned} ownership update(s) whose object never spawned.");
		}
	}
}
