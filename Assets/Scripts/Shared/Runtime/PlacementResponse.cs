using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public struct PlacementResponse : INetworkSerializable
    {
        public FixedString64Bytes PlaceableTypeId;
        public Vector2Int GridPos;
        public bool Accepted;
        public FixedString64Bytes Code;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlaceableTypeId);
            serializer.SerializeValue(ref GridPos);
            serializer.SerializeValue(ref Accepted);
            serializer.SerializeValue(ref Code);
        }

        public static PlacementResponse Create(string placeableTypeId, Vector2Int gridPos, bool accepted, string code)
        {
            return new PlacementResponse
            {
                PlaceableTypeId = placeableTypeId,
                GridPos = gridPos,
                Accepted = accepted,
                Code = code
            };
        }
    }
}
