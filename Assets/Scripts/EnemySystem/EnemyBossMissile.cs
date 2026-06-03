using JetSimulation.Core;
using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class EnemyBossMissile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float damage;
        private float deathTime;

        public void Initialize(Vector3 moveDirection, float moveSpeed, float missileDamage, float lifetime)
        {
            direction = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector3.back;
            speed = Mathf.Max(0f, moveSpeed);
            damage = Mathf.Max(0f, missileDamage);
            deathTime = Time.time + Mathf.Max(0.1f, lifetime);
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;

            if (Time.time >= deathTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamagePlayer(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDamagePlayer(collision.gameObject);
        }

        private void TryDamagePlayer(GameObject hitObject)
        {
            var playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.TakeDamage(damage, gameObject);
            Debug.Log($"[EnemySystem] Boss missile hit player. Damage: {damage}");
            Destroy(gameObject);
        }
    }
}
