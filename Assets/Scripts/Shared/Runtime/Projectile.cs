using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Shared.Entities;

namespace Shared.Runtime
{
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NetworkObject))]
    public class Projectile : EntityRuntime
    {
        private const float DEFAULT_SPEED = 30f;
        private const float MAX_DISTANCE = 100f;

        private Vector3 direction;
        private float speed;
        private float damage;
        private float traveledDistance;
        private float spawnTime;

        public Vector3 Direction => direction;
        public float Speed => speed;
        public float Damage => damage;

        public void Initialize(Vector3 targetPosition, float projectileSpeed, float projectileDamage)
        {
            var startPos = transform.position;
            var endPos = targetPosition + Vector3.up * 0.5f;

            direction = (endPos - startPos).normalized;
            direction.y = 0f;
            direction.Normalize();

            speed = projectileSpeed > 0 ? projectileSpeed : DEFAULT_SPEED;
            damage = projectileDamage;
            traveledDistance = 0f;
            spawnTime = Time.time;

            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            if (Time.time - spawnTime > 3f || traveledDistance >= MAX_DISTANCE)
            {
                Despawn();
                return;
            }

            var moveDistance = speed * Time.deltaTime;
            var hit = Physics.Raycast(transform.position, direction, out var hitInfo, moveDistance + 0.5f);

            if (hit)
            {
                var tower = hitInfo.collider.GetComponent<TowerRuntime>();
                if (tower != null)
                {
                    tower.TakeDamageServerRpc(damage);
                }

                Despawn();
                return;
            }

            transform.position += direction * moveDistance;
            traveledDistance += moveDistance;
        }

        public void Despawn()
        {
            if (!IsServer || !IsSpawned) return;
            NetworkObject.Despawn();
        }
    }
}
