using Unity.Netcode;

namespace Game.Shared.Runtime
{
    public class WorldGenerationState : NetworkBehaviour
    {
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
        public int MinDistanceFromEdge => minDistanceFromEdge.Value;
        public float WaterThreshold => waterThreshold.Value;
        public float WaterNoiseScale => waterNoiseScale.Value;
        public int WaterSmoothPasses => waterSmoothPasses.Value;

        public void SetServerValues(int seed, int minEdge, float threshold, float noiseScale, int smoothPasses)
        {
            if (!IsServer)
                return;

            replicatedSeed.Value = seed;
            minDistanceFromEdge.Value = minEdge;
            waterThreshold.Value = threshold;
            waterNoiseScale.Value = noiseScale;
            waterSmoothPasses.Value = smoothPasses;
        }
    }
}
