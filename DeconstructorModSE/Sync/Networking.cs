using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DeconstructorModSE.Sync
{
	public class Networking
	{
		public readonly ushort PacketId;

		public Networking(ushort packetID)
		{
			PacketId = packetID;

		}

		public void Register()
		{
			MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(PacketId, ReceivedPacket);
		}

		public void Unregister()
		{
			MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(PacketId, ReceivedPacket);
		}

		private void ReceivedPacket(ushort networkID, byte[] rawData, ulong steamID, bool isSenderServer) // executed when a packet is received on this machine
		{
			try
			{
				var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);

				bool relay = false;
				packet.Received(ref relay);

				if (relay)
					RelayToClients(packet, rawData);
			}
			catch (Exception e)
			{
				DeconstructorLog.Error(e);
			}
		}

		public void SendToServer(PacketBase packet)
		{
			var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

			MyAPIGateway.Multiplayer.SendMessageToServer(PacketId, bytes);
		}

		public void SendToPlayer(PacketBase packet, ulong steamId)
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
				return;

			var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

			MyAPIGateway.Multiplayer.SendMessageTo(PacketId, bytes, steamId);
		}

		private List<IMyPlayer> tempPlayers;

		public void RelayToClients(PacketBase packet, byte[] rawData = null)
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
				return;

			if (tempPlayers == null)
				tempPlayers = new List<IMyPlayer>(MyAPIGateway.Session.SessionSettings.MaxPlayers);
			else
				tempPlayers.Clear();

			MyAPIGateway.Players.GetPlayers(tempPlayers);

			foreach (var p in tempPlayers)
			{
				if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId)
					continue;

				if (p.SteamUserId == packet.SenderId)
					continue;

				if (rawData == null)
					rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);

				MyAPIGateway.Multiplayer.SendMessageTo(PacketId, rawData, p.SteamUserId);
			}

			tempPlayers.Clear();
		}
	}
}