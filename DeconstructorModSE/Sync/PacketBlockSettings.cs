using ProtoBuf;
using Sandbox.ModAPI;

namespace DeconstructorModSE.Sync
{
	[ProtoContract(UseProtoMembersOnly = true)]
	public class PacketBlockSettings : PacketBase
	{
		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public DeconstructorBlockSettings Settings;

		public PacketBlockSettings() { }

        public void Send(long entityId, DeconstructorBlockSettings settings)
        {
            EntityId = entityId;
            Settings = settings;

            if (MyAPIGateway.Multiplayer.IsServer)
                Networking.RelayToClients(this);
            else
                Networking.SendToServer(this);
        }

        public override void Received(ref bool relay)
        {
            var block = MyAPIGateway.Entities.GetEntityById(this.EntityId) as IMyCollector;

            if (block == null)
                return;

            var logic = block.GameLogic?.GetAs<DeconstructorMod>();

            if (logic == null)
                return;

            logic.Settings.Efficiency = this.Settings.Efficiency;

            relay = true;
        }
    }
}
