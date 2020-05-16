using ProtoBuf;
using Sandbox.ModAPI;

namespace DeconstructorModSE.Sync
{
    [ProtoInclude(1000, typeof(PacketClient))]
    [ProtoInclude(1001, typeof(PacketServer))]
    [ProtoContract(UseProtoMembersOnly = true)]
    public abstract class PacketBase
    {
        [ProtoMember(1)]
        public readonly ulong SenderId;
    
        protected Networking Networking => DeconstructorSession.Instance.Net;
    
        public PacketBase()
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
        }
        public abstract void Received(ref bool relay);
    }
}
