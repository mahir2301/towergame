using System.Collections.Generic;
using Shared;
using Shared.Data;
using Shared.Entities;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerRuntime : NetworkBehaviour
    {
        private const float DefaultMoveSpeed = 10f;
        private const float DefaultRotationSpeed = 12f;
        private const float DefaultJumpHeight = 1f;
        private const float GroundCheckDistance = 1.1f;
        private const double MaxCommandSkewSeconds = 2.0;

        public static PlayerRuntime LocalPlayer { get; private set; }

        private readonly NetworkVariable<int> currentWeaponIndex = new(-1);
        private readonly NetworkVariable<ulong> connectedEnergyId = new(ulong.MaxValue);
        private readonly NetworkVariable<float> currentWeaponCooldown = new(0f);
        private readonly NetworkVariable<float> networkMoveSpeed = new(0f);

        private readonly List<WeaponInstance> weaponInstances = new();
        private Vector2 serverMoveInput;
        private Vector3 serverLookTarget;
        private bool hasLookTarget;
        private bool serverJumpRequested;
        private bool jumpExecuted;
        private uint nextMoveSequence;
        private uint nextActionSequence;
        private uint lastMoveSequence;
        private uint lastActionSequence;
        private bool hasMoveSequence;
        private bool hasActionSequence;

        [SerializeField] private float moveSpeed = DefaultMoveSpeed;
        [SerializeField] private float rotationSpeed = DefaultRotationSpeed;
        [SerializeField] private float jumpHeight = DefaultJumpHeight;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private Animator animator;

        private Rigidbody rigidBody;

        public int CurrentWeaponIndex => currentWeaponIndex.Value;
        public ulong ConnectedEnergyId => connectedEnergyId.Value;
        public float CurrentWeaponCooldown => currentWeaponCooldown.Value;
        public float NetworkMoveSpeed => networkMoveSpeed.Value;
        public IReadOnlyList<WeaponInstance> WeaponInstances => weaponInstances;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            rigidBody = GetComponent<Rigidbody>();

            if (IsOwner)
            {
                LocalPlayer = this;
            }

            if (IsServer)
            {
                InitializeWeapons();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsOwner && LocalPlayer == this)
            {
                LocalPlayer = null;
            }
        }

        private void Update()
        {
            if (IsServer && IsSpawned)
            {
                SimulateMovement(Time.deltaTime);

                foreach (var weapon in weaponInstances)
                {
                    weapon.TickCooldown(Time.deltaTime);
                }

                var currentWeapon = GetCurrentWeapon();
                currentWeaponCooldown.Value = currentWeapon?.CurrentCooldown ?? 0f;
            }

            if (animator != null)
                animator.SetFloat("Speed", networkMoveSpeed.Value);
        }

        private void InitializeWeapons()
        {
            var registry = GameRegistry.Instance;
            if (registry == null) return;

            var weaponTypes = registry.WeaponTypes;

            for (var i = 0; i < weaponTypes.Count && i < 3; i++)
            {
                var weapon = weaponTypes[i];
                if (weapon == null) continue;

                var instance = new WeaponInstance(weapon);
                weaponInstances.Add(instance);
            }

            if (weaponInstances.Count > 0)
                currentWeaponIndex.Value = 0;
        }

        public WeaponInstance GetCurrentWeapon()
        {
            var index = currentWeaponIndex.Value;
            if (index < 0 || index >= weaponInstances.Count)
                return null;
            return weaponInstances[index];
        }

        public void SubmitMoveCommand(Vector2 moveInput, Vector3 lookTarget, bool jumpRequested)
        {
            SubmitMoveCommandServerRpc(new PlayerMoveCommand
            {
                Sequence = ++nextMoveSequence,
                ClientTime = GetClientCommandTime(),
                JumpRequested = jumpRequested,
                MoveInput = moveInput,
                LookTarget = lookTarget,
            });
        }

        public void SubmitFireCommand(Vector3 targetPosition)
        {
            SubmitActionCommandServerRpc(new PlayerActionCommand
            {
                Sequence = ++nextActionSequence,
                ClientTime = GetClientCommandTime(),
                Kind = PlayerActionKind.Fire,
                TargetPosition = targetPosition,
            });
        }

        public void SubmitSwitchWeaponCommand(int index)
        {
            SubmitActionCommandServerRpc(new PlayerActionCommand
            {
                Sequence = ++nextActionSequence,
                ClientTime = GetClientCommandTime(),
                Kind = PlayerActionKind.SwitchWeapon,
                WeaponIndex = index,
            });
        }

        public void SubmitConnectEnergyCommand(ulong energyId)
        {
            SubmitActionCommandServerRpc(new PlayerActionCommand
            {
                Sequence = ++nextActionSequence,
                ClientTime = GetClientCommandTime(),
                Kind = PlayerActionKind.ConnectEnergy,
                EnergyId = energyId,
            });
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitMoveCommandServerRpc(PlayerMoveCommand command, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!HasCommandAuthority(rpcParams.Receive.SenderClientId)) return;
            if (!IsMoveCommandFresh(command)) return;

            lastMoveSequence = command.Sequence;
            hasMoveSequence = true;
            serverMoveInput = Vector2.ClampMagnitude(command.MoveInput, 1f);
            serverLookTarget = command.LookTarget;
            hasLookTarget = true;
            if (command.JumpRequested && !serverJumpRequested)
                serverJumpRequested = true;
        }

        private void SpawnProjectile(Vector3 targetPosition, WeaponType config)
        {
            var startPos = transform.position + Vector3.up * 1.5f;
            if (!EntityManager.TrySpawnByKind(EntityKind.Projectile, startPos, Quaternion.identity, ulong.MaxValue,
                    out var projectileEntity))
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Failed to spawn projectile entity for player netId={NetworkObjectId}.");
                return;
            }

            var projectile = projectileEntity as Projectile;
            if (projectile == null)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Spawned projectile entity id={projectileEntity.EntityId} does not have Projectile runtime.");
                projectileEntity.NetworkObject.Despawn(true);
                return;
            }

            projectile.Initialize(targetPosition, config.Stats.projectileSpeed, config.Stats.damage);
        }

        [ClientRpc]
        private void FireWeaponClientRpc(Vector3 targetPosition, string weaponId)
        {
            if (!IsOwner) return;
            GameEvents.RaiseWeaponFired(this, targetPosition, weaponId);
        }

        [Rpc(SendTo.Owner)]
        private void ReportActionResultClientRpc(PlayerActionKind actionKind, PlayerActionResult result)
        {
            if (!IsOwner)
                return;

            GameEvents.RaisePlayerActionResultReceived(this, actionKind, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitActionCommandServerRpc(PlayerActionCommand command, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!HasCommandAuthority(rpcParams.Receive.SenderClientId)) return;
            if (!IsActionCommandFresh(command))
            {
                ReportActionResultClientRpc(command.Kind, PlayerActionResult.RejectedStale);
                return;
            }

            if (!IsActionCommandValid(command))
            {
                ReportActionResultClientRpc(command.Kind, PlayerActionResult.RejectedInvalidPayload);
                return;
            }

            lastActionSequence = command.Sequence;
            hasActionSequence = true;

            var result = PlayerActionResult.RejectedInvalidPayload;
            switch (command.Kind)
            {
                case PlayerActionKind.Fire:
                    result = HandleFireCommand(command.TargetPosition);
                    break;
                case PlayerActionKind.SwitchWeapon:
                    result = HandleSwitchWeaponCommand(command.WeaponIndex);
                    break;
                case PlayerActionKind.ConnectEnergy:
                    result = HandleConnectEnergyCommand(command.EnergyId);
                    break;
                default:
                    result = PlayerActionResult.RejectedInvalidPayload;
                    break;
            }

            ReportActionResultClientRpc(command.Kind, result);
        }

        private PlayerActionResult HandleSwitchWeaponCommand(int index)
        {
            if (!IsCombatPhase())
                return PlayerActionResult.RejectedOutOfPhase;

            if (index < 0 || index >= weaponInstances.Count)
                return PlayerActionResult.RejectedInvalidPayload;

            currentWeaponIndex.Value = index;
            GameEvents.RaiseWeaponSwitched(this, index);
            return PlayerActionResult.Accepted;
        }

        private PlayerActionResult HandleFireCommand(Vector3 targetPosition)
        {
            if (!IsCombatPhase())
                return PlayerActionResult.RejectedOutOfPhase;

            var weapon = GetCurrentWeapon();
            if (weapon == null)
                return PlayerActionResult.RejectedNoWeapon;

            if (!weapon.CanFire())
                return PlayerActionResult.RejectedCooldown;

            var config = weapon.GetConfig();
            if (config == null)
                return PlayerActionResult.RejectedInvalidConfig;

            if (!TryConsumeEnergy(config.Stats.energyCostPerShot, config.ClassType))
                return PlayerActionResult.RejectedInsufficientEnergy;

            weapon.StartCooldown();

            var direction = (targetPosition - transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            SpawnProjectile(targetPosition, config);
            FireWeaponClientRpc(targetPosition, config.Id);
            return PlayerActionResult.Accepted;
        }

        private PlayerActionResult HandleConnectEnergyCommand(ulong energyId)
        {
            if (energyId == ulong.MaxValue)
            {
                connectedEnergyId.Value = ulong.MaxValue;
                return PlayerActionResult.Accepted;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(energyId, out var obj))
                return PlayerActionResult.RejectedInvalidEnergyTarget;

            var energy = obj.GetComponent<EnergyRuntime>();
            if (energy == null || !energy.IsSpawned)
                return PlayerActionResult.RejectedInvalidEnergyTarget;

            connectedEnergyId.Value = energyId;
            return PlayerActionResult.Accepted;
        }

        private bool TryConsumeEnergy(int amount, ClassType classType)
        {
            if (amount <= 0) return true;
            if (connectedEnergyId.Value == ulong.MaxValue) return false;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(connectedEnergyId.Value, out var obj))
                return false;

            var energy = obj.GetComponent<EnergyRuntime>();
            if (energy == null || !energy.IsSpawned) return false;
            if (!energy.CanConnectClass(classType)) return false;
            if (!energy.HasCapacity(amount)) return false;

            return energy.TryConnectTower(NetworkObjectId, amount);
        }

        private void SimulateMovement(float deltaTime)
        {
            var currentVelocity = rigidBody.linearVelocity;
            var movement = new Vector3(serverMoveInput.x, 0f, serverMoveInput.y);
            if (movement.sqrMagnitude > 1f)
                movement.Normalize();

            var planarVelocity = movement * moveSpeed;
            currentVelocity.x = planarVelocity.x;
            currentVelocity.z = planarVelocity.z;
            rigidBody.linearVelocity = currentVelocity;

            if (hasLookTarget)
            {
                var lookDir = serverLookTarget - rigidBody.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    var targetRotation = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
                }
            }

            HandleJump();
            networkMoveSpeed.Value = movement.magnitude * moveSpeed;
        }

        private void HandleJump()
        {
            if (IsGrounded())
                jumpExecuted = false;

            if (!serverJumpRequested || jumpExecuted)
                return;

            serverJumpRequested = false;
            jumpExecuted = true;

            var velocity = rigidBody.linearVelocity;
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            rigidBody.linearVelocity = velocity;
        }

        private bool IsGrounded()
        {
            var origin = rigidBody.position + Vector3.up * 0.05f;
            return Physics.Raycast(origin, Vector3.down, GroundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
        }

        private bool HasCommandAuthority(ulong senderClientId)
        {
            if (senderClientId == OwnerClientId)
                return true;

            RuntimeLog.Entity.Warning(RuntimeLog.Code.EntityOwnershipRejected,
                $"Rejected player command for netId={NetworkObjectId} from client {senderClientId}; owner is {OwnerClientId}.");
            return false;
        }

        private bool IsMoveCommandFresh(PlayerMoveCommand command)
        {
            if (hasMoveSequence && !IsSequenceNewer(command.Sequence, lastMoveSequence))
                return false;

            return IsCommandTimestampValid(command.ClientTime);
        }

        private bool IsActionCommandFresh(PlayerActionCommand command)
        {
            if (hasActionSequence && !IsSequenceNewer(command.Sequence, lastActionSequence))
                return false;

            return IsCommandTimestampValid(command.ClientTime);
        }

        private bool IsActionCommandValid(PlayerActionCommand command)
        {
            switch (command.Kind)
            {
                case PlayerActionKind.Fire:
                    if (float.IsNaN(command.TargetPosition.x) || float.IsNaN(command.TargetPosition.y)
                        || float.IsNaN(command.TargetPosition.z) || float.IsInfinity(command.TargetPosition.x)
                        || float.IsInfinity(command.TargetPosition.y) || float.IsInfinity(command.TargetPosition.z))
                    {
                        RuntimeLog.Entity.Warning(RuntimeLog.Code.EntityActionRejected,
                            $"Rejected fire command for netId={NetworkObjectId}: invalid target position.");
                        return false;
                    }

                    return true;

                case PlayerActionKind.SwitchWeapon:
                    if (command.WeaponIndex >= 0 && command.WeaponIndex < weaponInstances.Count)
                        return true;

                    RuntimeLog.Entity.Warning(RuntimeLog.Code.EntityActionRejected,
                        $"Rejected switch-weapon command for netId={NetworkObjectId}: index {command.WeaponIndex} is invalid.");
                    return false;

                case PlayerActionKind.ConnectEnergy:
                    return true;

                default:
                    RuntimeLog.Entity.Warning(RuntimeLog.Code.EntityActionRejected,
                        $"Rejected action command for netId={NetworkObjectId}: unsupported action '{command.Kind}'.");
                    return false;
            }
        }

        private static bool IsCombatPhase()
        {
            return PhaseManager.Instance != null && PhaseManager.Instance.CurrentPhase == GamePhase.Combat;
        }

        private bool IsCommandTimestampValid(double clientTime)
        {
            if (clientTime <= 0d)
                return false;

            var serverTime = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.unscaledTimeAsDouble;
            return Mathf.Abs((float)(serverTime - clientTime)) <= MaxCommandSkewSeconds;
        }

        private static bool IsSequenceNewer(uint next, uint previous)
        {
            var diff = next - previous;
            return diff != 0 && diff < 0x80000000;
        }

        private static double GetClientCommandTime()
        {
            return NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalTime.Time
                : Time.unscaledTimeAsDouble;
        }
    }
}
