using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Shared.Runtime
{
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NetworkObject))]
    public class Projectile : NetworkBehaviour
    {
        private float speed = 30f;
        private float lifetime = 3f;
        private float damage = 10f;
        private Vector3 direction;
        private float traveledDistance;
        private float maxDistance = 100f;

        public void Initialize(Vector3 targetPosition, float projectileSpeed, float projectileDamage)
        {
            var startPos = transform.position;
            var endPos = targetPosition + Vector3.up * 0.5f;
            direction = (endPos - startPos).normalized;
            direction.y = 0;
            direction.Normalize();

            speed = projectileSpeed;
            damage = projectileDamage;
            traveledDistance = 0f;

            transform.rotation = Quaternion.LookRotation(direction);

            Debug.Log($"[Projectile] Initialize - startPos: {startPos}, target: {targetPosition}");
            Debug.Log($"[Projectile] Direction: {direction}, Speed: {speed}, DeltaTime will be: {Time.deltaTime}");
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!IsSpawned) return;

            var moveDistance = speed * Time.deltaTime;
            traveledDistance += moveDistance;

            Debug.Log($"[Projectile] Update - Pos: {transform.position}, MoveDist: {moveDistance}, Speed: {speed}, DT: {Time.deltaTime}");

            if (traveledDistance >= maxDistance)
            {
                Despawn();
                return;
            }

            transform.position += direction * moveDistance;
            Debug.Log($"[Projectile] New Pos: {transform.position}");

            if (Physics.Raycast(transform.position, direction, out var hit, moveDistance + 0.5f))
            {
                var tower = hit.collider.GetComponent<TowerRuntime>();
                if (tower != null)
                {
                    tower.TakeDamageServerRpc(damage);
                }

                Despawn();
            }
        }

        private void Despawn()
        {
            if (IsServer && IsSpawned)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }
}
