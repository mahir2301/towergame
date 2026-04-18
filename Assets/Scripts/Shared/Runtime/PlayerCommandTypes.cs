using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public enum PlayerActionKind : byte
    {
        None = 0,
        Fire = 1,
        SwitchWeapon = 2,
        ConnectEnergy = 3,
    }

    public struct PlayerMoveCommand : INetworkSerializable
    {
        public uint Sequence;
        public double ClientTime;
        public bool JumpRequested;
        public Vector2 MoveInput;
        public Vector3 LookTarget;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref ClientTime);
            serializer.SerializeValue(ref JumpRequested);
            serializer.SerializeValue(ref MoveInput);
            serializer.SerializeValue(ref LookTarget);
        }
    }

    public struct PlayerActionCommand : INetworkSerializable
    {
        public uint Sequence;
        public double ClientTime;
        public PlayerActionKind Kind;
        public Vector3 TargetPosition;
        public int WeaponIndex;
        public ulong EnergyId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref ClientTime);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref TargetPosition);
            serializer.SerializeValue(ref WeaponIndex);
            serializer.SerializeValue(ref EnergyId);
        }
    }
}
