using Unity.Netcode;
using UnityEngine;
using Shared.Utilities;

namespace Shared
{
    public class PhaseManager : NetworkBehaviour
    {
        public static PhaseManager Instance { get; private set; }

        private readonly NetworkVariable<GamePhase> currentPhase = new(GamePhase.Building);
        private bool subscribedToPhaseChanges;

        public GamePhase CurrentPhase => currentPhase.Value;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!subscribedToPhaseChanges)
            {
                currentPhase.OnValueChanged += HandlePhaseChanged;
                subscribedToPhaseChanges = true;
            }

            GameEvents.RaisePhaseChanged(currentPhase.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (subscribedToPhaseChanges)
            {
                currentPhase.OnValueChanged -= HandlePhaseChanged;
                subscribedToPhaseChanges = false;
            }
        }

        private void HandlePhaseChanged(GamePhase previousValue, GamePhase newValue)
        {
            RuntimeLog.Phase.Info(RuntimeLog.Code.PhaseChanged,
                $"Changed: {previousValue} -> {newValue}");
            GameEvents.RaisePhaseChanged(newValue);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestSetPhaseRpc(GamePhase phase)
        {
            if (!IsServer)
            {
                return;
            }

            currentPhase.Value = phase;
        }
    }
}
