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
        private bool gameOverSubscribed;

        private void OnEnable()
        {
            TrySubscribeGameOver();
        }

        private void OnDisable()
        {
            UnsubscribeGameOver();
        }

        public void Initialize(Vector3 moveDirection, float moveSpeed, float missileDamage, float lifetime)
        {
            direction = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector3.back;
            speed = Mathf.Max(0f, moveSpeed);
            damage = Mathf.Max(0f, missileDamage);
            deathTime = Time.time + Mathf.Max(0.1f, lifetime);
        }

        private void Update()
        {
            TrySubscribeGameOver();

            if (IsGameOver())
            {
                DestroyForGameOver();
                return;
            }

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

        public void DestroyForGameOver()
        {
            Destroy(gameObject);
        }

        private void TrySubscribeGameOver()
        {
            if (gameOverSubscribed || GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.OnGameOver += DestroyForGameOver;
            gameOverSubscribed = true;
        }

        private void UnsubscribeGameOver()
        {
            if (!gameOverSubscribed || GameManager.Instance == null)
            {
                gameOverSubscribed = false;
                return;
            }

            GameManager.Instance.OnGameOver -= DestroyForGameOver;
            gameOverSubscribed = false;
        }

        private static bool IsGameOver()
        {
            return GameManager.Instance != null && GameManager.Instance.IsGameOver;
        }
    }
}
