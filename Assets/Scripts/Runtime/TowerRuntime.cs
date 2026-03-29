using Data;
using Unity.Netcode;
using UnityEngine;

namespace Runtime
{
    public class TowerRuntime : NetworkBehaviour, IPlaceable
    {
        [Header("Configuration")]
        [SerializeField]
        private string configId;

        [Header("State")]
        [SerializeField]
        private Vector2Int gridPosition;
        [SerializeField]
        private Vector2Int size = new(1, 1);

        private NetworkVariable<float> currentHealth = new();
        private NetworkVariable<bool> isPowered = new();

        private TowerType cachedConfig;

        public string ConfigId => configId;
        public Vector2Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }
        public Vector2Int Size => size;
        public float CurrentHealth => currentHealth.Value;
        public bool IsPowered => isPowered.Value;

        public void Initialize(Vector2Int gridPos)
        {
            gridPosition = gridPos;
        }

        public void Initialize(string towerConfigId, Vector2Int gridPos, Vector2Int towerSize, Vector3 worldPosition)
        {
            configId = towerConfigId;
            gridPosition = gridPos;
            size = towerSize;
            cachedConfig = GetConfig();

            if (IsServer && cachedConfig != null)
            {
                currentHealth.Value = cachedConfig.Stats.maxHealth;
            }

            transform.position = worldPosition;
        }

        public TowerType GetConfig()
        {
            if (cachedConfig == null && !string.IsNullOrEmpty(configId))
            {
                cachedConfig = GameRegistry.Instance?.GetTowerType(configId);
            }

            return cachedConfig;
        }
        
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageServerRpc(float amount)
        {
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
        }
        
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RepairServerRpc(float amount)
        {
            currentHealth.Value = Mathf.Min(cachedConfig.Stats.maxHealth, currentHealth.Value + amount);
        }

        public void SetPowered(bool powered)
        {
            if (IsServer)
            {
                isPowered.Value = powered;
            }
        }
    }
}