using ProtoBuf;
using Sandbox.ModAPI;

namespace DeconstructorModSE.Sync
{
	[ProtoContract]
	public class PacketServer : PacketBase
	{
		public PacketServer()
		{
		}

		[ProtoMember(1)]
		public long EntityId;

		[ProtoMember(2)]
		public long TargetId;

		[ProtoMember(3)]
		public float Efficiency;

		public void Send(long entityId, long targetId, float Eff)
		{
			EntityId = entityId;
			TargetId = targetId;
			Efficiency = Eff;

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

			logic.Settings.Efficiency = Efficiency;
			logic.SyncServer(TargetId);

			relay = false;
		}
	}
}