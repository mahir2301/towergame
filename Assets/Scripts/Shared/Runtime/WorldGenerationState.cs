using System;
using Shared.Utilities;
using Unity.Collections;
using Unity.Netcode;

namespace Shared.Runtime
{
    public class WorldGenerationState : NetworkBehaviour
    {
        private event Action stateChanged;

        public event Action ServerSpawned;
        public event Action BecameReady;

        private readonly NetworkVariable<int> replicatedSeed = new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> minDistanceFromEdge = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> waterThreshold = new(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> waterNoiseScale = new(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> waterSmoothPasses = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> tileMapChecksum = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkList<FixedString64Bytes> tileTypeIds = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public int ReplicatedSeed => replicatedSeed.Value;
        public NetworkVariable<int> Seed => replicatedSeed;
        public int MinDistanceFromEdge => minDistanceFromEdge.Value;
        public float WaterThreshold => waterThreshold.Value;
        public float WaterNoiseScale => waterNoiseScale.Value;
        public int WaterSmoothPasses => waterSmoothPasses.Value;
        public int TileMapChecksum => tileMapChecksum.Value;
        public bool HasValidState => replicatedSeed.Value >= 0;

        public void SubscribeStateChanged(Action listener, bool replayCurrentState = true)
        {
            if (listener == null)
                return;

            stateChanged += listener;
            if (replayCurrentState && IsSpawned && HasValidState)
                listener();
        }

        public void UnsubscribeStateChanged(Action listener)
        {
            if (listener == null)
                return;

            stateChanged -= listener;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            replicatedSeed.OnValueChanged += HandleStateValueChanged;
            minDistanceFromEdge.OnValueChanged += HandleStateValueChanged;
            waterThreshold.OnValueChanged += HandleStateValueChanged;
            waterNoiseScale.OnValueChanged += HandleStateValueChanged;
            waterSmoothPasses.OnValueChanged += HandleStateValueChanged;
            tileMapChecksum.OnValueChanged += HandleStateValueChanged;
            tileTypeIds.OnListChanged += HandleTileListChanged;

            if (IsServer)
                ServerSpawned?.Invoke();

            if (HasValidState)
            {
                BecameReady?.Invoke();
                stateChanged?.Invoke();
            }
        }

        public override void OnNetworkDespawn()
        {
            replicatedSeed.OnValueChanged -= HandleStateValueChanged;
            minDistanceFromEdge.OnValueChanged -= HandleStateValueChanged;
            waterThreshold.OnValueChanged -= HandleStateValueChanged;
            waterNoiseScale.OnValueChanged -= HandleStateValueChanged;
            waterSmoothPasses.OnValueChanged -= HandleStateValueChanged;
            tileMapChecksum.OnValueChanged -= HandleStateValueChanged;
            tileTypeIds.OnListChanged -= HandleTileListChanged;

            base.OnNetworkDespawn();
        }

        public void SetServerValues(int seed, int minEdge, float threshold, float noiseScale, int smoothPasses)
        {
            if (!RuntimeNet.IsServer)
                return;

            minDistanceFromEdge.Value = minEdge;
            waterThreshold.Value = threshold;
            waterNoiseScale.Value = noiseScale;
            waterSmoothPasses.Value = smoothPasses;
            replicatedSeed.Value = seed;
        }

        public void SetServerTileMap(string[] flattenedTileTypeIds)
        {
            if (!RuntimeNet.IsServer)
                return;

            tileTypeIds.Clear();
            if (flattenedTileTypeIds == null)
            {
                tileMapChecksum.Value = 0;
                return;
            }

            var checksum = 17;
            for (var i = 0; i < flattenedTileTypeIds.Length; i++)
            {
                var id = flattenedTileTypeIds[i] ?? string.Empty;
                tileTypeIds.Add(new FixedString64Bytes(id));
                checksum = unchecked(checksum * 31 + id.GetHashCode());
            }

            tileMapChecksum.Value = checksum;
        }

        public string[] GetTileTypeIdMap()
        {
            var result = new string[tileTypeIds.Count];
            for (var i = 0; i < tileTypeIds.Count; i++)
                result[i] = tileTypeIds[i].ToString();

            return result;
        }

        private void HandleStateValueChanged<T>(T previousValue, T newValue)
        {
            if (!HasValidState)
                return;

            BecameReady?.Invoke();
            stateChanged?.Invoke();
        }

        private void HandleTileListChanged(NetworkListEvent<FixedString64Bytes> _)
        {
            if (!HasValidState)
                return;

            BecameReady?.Invoke();
            stateChanged?.Invoke();
        }

        public override void OnDestroy()
        {
            stateChanged = null;
            ServerSpawned = null;
            BecameReady = null;

            base.OnDestroy();
        }
    }
}
