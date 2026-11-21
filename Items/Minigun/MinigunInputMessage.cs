using Mirror;
using UnityEngine;

namespace IssaPlugin.Items
{
    public struct MinigunInputMessage : NetworkMessage
    {
        public bool Held;
        public bool SingleShot;
        public int Slot;
        public int Seed;
        public Vector3 Direction;
    }
    public static class MinigunInputSerialization
    {
        public static void Write(NetworkWriter writer, MinigunInputMessage value)
        {
            writer.WriteBool(value.Held); writer.WriteBool(value.SingleShot); writer.WriteInt(value.Slot);
            writer.WriteInt(value.Seed); writer.WriteVector3(value.Direction);
        }
        public static MinigunInputMessage Read(NetworkReader reader) => new MinigunInputMessage
        {
            Held = reader.ReadBool(), SingleShot = reader.ReadBool(), Slot = reader.ReadInt(), Seed = reader.ReadInt(), Direction = reader.ReadVector3()
        };
    }
}
