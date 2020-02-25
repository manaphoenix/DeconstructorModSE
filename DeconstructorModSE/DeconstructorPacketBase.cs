using ProtoBuf;
using Sandbox.ModAPI;

namespace DeconstructorModSE
{
	// tag numbers in ProtoInclude collide with numbers from ProtoMember in the same class, therefore they must be unique.
	[ProtoInclude(1000, typeof(DeconstructorPacketData))]
	[ProtoContract]
	public abstract class DeconstructorPacketBase
	{
        [ProtoMember(1)]
        public readonly ulong SenderId;
        public DeconstructorPacketBase()
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
        }

        public abstract bool Received();
    }
}
