using System;
using Unity.Netcode;

namespace Shared.Runtime
{
    public class WorldGenerationState : NetworkBehaviour
    {
        private event Action stateChanged;

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

        public int ReplicatedSeed => replicatedSeed.Value;
        public NetworkVariable<int> Seed => replicatedSeed;
        public int MinDistanceFromEdge => minDistanceFromEdge.Value;
        public float WaterThreshold => waterThreshold.Value;
        public float WaterNoiseScale => waterNoiseScale.Value;
        public int WaterSmoothPasses => waterSmoothPasses.Value;
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

            if (HasValidState)
                stateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            replicatedSeed.OnValueChanged -= HandleStateValueChanged;
            minDistanceFromEdge.OnValueChanged -= HandleStateValueChanged;
            waterThreshold.OnValueChanged -= HandleStateValueChanged;
            waterNoiseScale.OnValueChanged -= HandleStateValueChanged;
            waterSmoothPasses.OnValueChanged -= HandleStateValueChanged;

            base.OnNetworkDespawn();
        }

        public void SetServerValues(int seed, int minEdge, float threshold, float noiseScale, int smoothPasses)
        {
            if (!IsServer)
                return;

            minDistanceFromEdge.Value = minEdge;
            waterThreshold.Value = threshold;
            waterNoiseScale.Value = noiseScale;
            waterSmoothPasses.Value = smoothPasses;
            replicatedSeed.Value = seed;
        }

        private void HandleStateValueChanged<T>(T previousValue, T newValue)
        {
            if (!HasValidState)
                return;

            stateChanged?.Invoke();
        }
    }
}
