using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DeconstructorModSE
{
    [ProtoContract]
    public class DeconstructorPacketClient : DeconstructorPacketBase
    {
        public DeconstructorPacketClient() { }

        
        [ProtoMember(1)]
        public bool IsGrinding;

        [ProtoMember(2)]
        public long DeconId;

        [ProtoMember(3)]
        public int Eff;

        [ProtoMember(4)]
        public float Time;

        public DeconstructorPacketClient(long block, bool grinding, int eff, float time)
        {
            IsGrinding = grinding;
            DeconId = block;
            Eff = eff;
            Time = time;
        }

        public override bool Received()
        {
            //TODO CLIENT REC MSG
            IMyEntity entity;
            MyAPIGateway.Entities.TryGetEntityById(DeconId, out entity);

            if (entity != null && !entity.MarkedForClose)
            {
                var system = entity?.GameLogic?.GetAs<DeconstructorMod>();
                if (system != null)
                {
                    system.SyncClient(IsGrinding,Eff,Time);
                }
            }

            return false;
        }
    }
}
