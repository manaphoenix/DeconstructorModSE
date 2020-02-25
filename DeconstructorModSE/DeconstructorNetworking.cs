using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
	public class DeconstructorNetworking
	{
        public readonly ushort UUID; //Unique ID for networking.

        private List<IMyPlayer> tempPlayers = null;
        public DeconstructorNetworking(ushort CID)
        {
            UUID = CID;
        }

        public void Register()
        {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(UUID, ReceivedPacket);
        }

        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(UUID, ReceivedPacket);
        }

        private void ReceivedPacket(byte[] rawData)
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<DeconstructorPacketBase>(rawData);

                HandlePacket(packet, rawData);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author]", 10000, MyFontEnum.Red);
            }
        }

        private void HandlePacket(DeconstructorPacketBase packet, byte[] rawData = null)
        {
            var relay = packet.Received();

            if (relay)
                RelayToClients(packet, rawData);
        }

        public void SendToServer(DeconstructorPacketBase packet)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                HandlePacket(packet);
                return;
            }

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageToServer(UUID, bytes);
        }

        public void SendToPlayer(DeconstructorPacketBase packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageTo(UUID, bytes, steamId);
        }

        public void RelayToClients(DeconstructorPacketBase packet, byte[] rawData = null)
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
                if (p.IsBot)
                    continue;

                if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId)
                    continue;

                if (p.SteamUserId == packet.SenderId)
                    continue;

                if (rawData == null)
                    rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);

                MyAPIGateway.Multiplayer.SendMessageTo(UUID, rawData, p.SteamUserId);
            }

            tempPlayers.Clear();
        }
    }
}
