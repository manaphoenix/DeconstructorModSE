using ProtoBuf;
using Sandbox.ModAPI;

namespace DeconstructorModSE.Sync
{
	[ProtoContract]
	public class PacketClient : PacketBase
	{
		public PacketClient()
		{
		}

		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public bool IsGrinding;

		[ProtoMember(3)]
		public float Efficiency;

		[ProtoMember(4)]
		public float Time;

		public void Send(long entityId, bool isGrinding, float Eff, float time)
		{
			EntityId = entityId;
			IsGrinding = isGrinding;
			Efficiency = Eff;
			Time = time;

			Networking.RelayToClients(this);
		}

		public override void Received(ref bool relay)
		{
			var block = MyAPIGateway.Entities.GetEntityById(this.EntityId) as IMyShipGrinder;

			if (block == null)
				return;

			var logic = block.GameLogic?.GetAs<DeconstructorMod>();

			if (logic == null)
				return;

			logic.Settings.Efficiency = Efficiency;
			logic.Settings.Time = Time;
			logic.Settings.IsGrinding = IsGrinding;

			relay = false;
		}
	}
}