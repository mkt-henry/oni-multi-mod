using System.IO;
using ONI_Together.Networking.Ownership;
using ONI_Together.Networking.Packets.Architecture;
using Shared.Profiling;

namespace ONI_Together.Networking.Packets.World
{
	/// <summary>
	/// Tells clients to stamp the same starting area the host just placed.
	/// </summary>
	/// <remarks>
	/// A client's world is the save it was sent when it joined, so terrain the host creates afterwards
	/// simply is not there - the pod existed and was owned correctly, and the client could see none of
	/// it.
	/// <para>
	/// Only the template path and position travel. The template itself is game data both machines
	/// already have, and building NetIds are derived from position, so stamping the same template at
	/// the same place produces a matching result on both sides. Resending the whole world through a
	/// hard sync would also work and costs megabytes; this costs a few dozen bytes.
	/// </para>
	/// <para>
	/// Ownership deliberately does not ride along. The host assigns it and
	/// <c>OwnershipSyncPacket</c> carries it, so the pod is owned by whoever the host says rather than
	/// by whoever the client guesses.
	/// </para>
	/// </remarks>
	public class StartingAreaStampPacket : IPacket
	{
		public string TemplatePath;
		public int X;
		public int Y;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(TemplatePath ?? string.Empty);
			writer.Write(X);
			writer.Write(Y);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			TemplatePath = reader.ReadString();
			X = reader.ReadInt32();
			Y = reader.ReadInt32();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			// The host already stamped its own copy; running it again there would double the terrain.
			if (MultiplayerSession.IsHost) return;

			if (string.IsNullOrEmpty(TemplatePath)) return;

			StartingAreaPlacer.StampFromNetwork(TemplatePath, X, Y);
		}
	}
}
