using ONI_Together.DebugTools;
using ONI_Together.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;

namespace ONI_Together.Networking.Packets.Core
{
	/// <summary>
	/// used by clients to broadcast a packet to all other clients via the host
	/// </summary>
	internal class HostBroadcastPacket : IPacket
	{
		public HostBroadcastPacket() { }
		public HostBroadcastPacket(IPacket innerPacket, ulong sender)
		{
			using var _ = Profiler.Scope();

			InnerPacketId = API_Helper.GetHashCode(innerPacket.GetType());
			using var ms = new MemoryStream();
			using var writer = new BinaryWriter(ms);
			innerPacket.Serialize(writer);
			InnerPacketData = ms.ToArray();
			SenderId = sender;
		}


		int InnerPacketId;
		public ulong SenderId;
		byte[] InnerPacketData;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(InnerPacketId);
			writer.Write(SenderId);
			writer.Write(InnerPacketData.Length);
			writer.Write(InnerPacketData);
		}
		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			InnerPacketId = reader.ReadInt32();
			SenderId = reader.ReadUInt64();
			int dataLength = reader.ReadInt32();
			InnerPacketData = reader.ReadBytes(dataLength);
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (!PacketRegistry.HasRegisteredPacket(InnerPacketId))
			{
				DebugConsole.LogWarning("[HostBroadcastPacket] unknown inner packet id found, cannot rebroadcast: "+InnerPacketId);
				return;
			}
			var innerPacket = PacketRegistry.Create(InnerPacketId);
			using var ms = new MemoryStream(InnerPacketData);
			using var reader = new BinaryReader(ms);
			innerPacket.Deserialize(reader);
			DebugConsole.Log("[HostBroadcastPacket] received packet of type " + innerPacket.GetType().Name+", dispatching");
			//this packet should only be sent by clients to the host
			if (MultiplayerSession.IsHost)
			{
				//trigger it on the host, attributed to whoever sent the envelope rather than to the envelope itself
				using (PacketContext.Scope(SenderId))
				{
					innerPacket.OnDispatched();
				}
				//send it to all other clients except the sender
				//note: the inner packet is relayed unwrapped, so those clients see it as coming from the host.
				//that is fine while permission checks live on the host; revisit if clients ever need attribution.
				PacketSender.SendToAllExcluding(innerPacket, [MultiplayerSession.HostUserID, SenderId]);
			}
		}

	}
}
