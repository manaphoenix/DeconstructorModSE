using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
    [ProtoContract]
    public class DeconstructorPacketData : DeconstructorPacketBase
    {
        public DeconstructorPacketData() { }

        
        [ProtoMember(1)]
        public long GridId;

        [ProtoMember(2)]
        public long DeconId;

        [ProtoMember(3)]
        public int Eff;

        public DeconstructorPacketData(long block, long grid, int eff)
        {
            GridId = grid;
            DeconId = block;
            Eff = eff;
        }

        public override bool Received()
        {
            //TODO SERVER REC MSG
            IMyEntity entity;
            MyAPIGateway.Entities.TryGetEntityById(DeconId, out entity);

            if (entity != null && !entity.MarkedForClose)
            {
                var system = entity?.GameLogic?.GetAs<DeconstructorMod>();
                if (system != null)
                {
                    system.SyncServer(GridId,Eff);
                }
            }

            return false;
        }
    }
}
