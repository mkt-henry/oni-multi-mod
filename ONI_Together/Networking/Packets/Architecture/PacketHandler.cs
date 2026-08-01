using System;
using System.IO;
using ONI_Together.DebugTools;
using ONI_Together.Networking;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Networking.Packets.Architecture
{

	public static class PacketHandler
	{
		private static bool _readyToProcess = true;
		private static float _notReadySince = float.MaxValue;
		private const float NOT_READY_TIMEOUT = 60f;

		public static bool readyToProcess
		{
			get => _readyToProcess;
			set
			{
				if (!value)
					_notReadySince = Time.unscaledTime;
				_readyToProcess = value;
			}
		}

		/// <summary>
		/// Kept for the public mod API. Prefer the overload that supplies the sender - without it the
		/// packet is dispatched unattributed and any permission check will treat it as untrusted.
		/// </summary>
		public static void HandleIncoming(byte[] data)
		{
			HandleIncoming(data, PacketContext.Unknown);
		}

		/// <param name="senderId">
		/// Player the packet arrived from, as reported by the transport. On a client this is the host.
		/// Pass <see cref="PacketContext.Unknown"/> only when the transport genuinely cannot attribute it.
		/// </param>
		public static void HandleIncoming(byte[] data, ulong senderId)
		{
			using var _ = Profiler.Scope();
			using var senderScope = PacketContext.Scope(senderId);

			if (!_readyToProcess)
			{
				if (Time.unscaledTime - _notReadySince > NOT_READY_TIMEOUT)
				{
					DebugConsole.LogWarning($"[PacketHandler] readyToProcess was false for >{NOT_READY_TIMEOUT}s — force-recovering");
					_readyToProcess = true;
				}
				else
				{
					return;
				}
			}

			using (var ms = new MemoryStream(data))
			{
				using (var reader = new BinaryReader(ms))
				{
					int type = (int)reader.ReadInt32();
                    if (!PacketRegistry.HasRegisteredPacket(type))
                    {
                        DebugConsole.LogError($"Invalid PacketType received: {type}", false);
                        return;
                    }

                    using var scope = Profiler.Scope();

                    var packet = PacketRegistry.Create(type);
					packet.Deserialize(reader);
					PacketSenderDiagnostics.Observe(senderId, packet.GetType().Name);
					Dispatch(packet);

                    scope.End(packet.GetType().Name, data.Length);

                    PacketTracker.TrackIncoming(new PacketTracker.PacketTrackData
                    {
						packet = packet,
						size = data.Length
                    });
                }
			}
		}

		private static void Dispatch(IPacket packet)
		{
			using var _ = Profiler.Scope();

			packet.OnDispatched();
		}
	}

}