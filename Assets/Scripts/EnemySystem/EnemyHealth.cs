using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, Damageable
    {
        [SerializeField] private float maxHealth = 10f;
        [SerializeField] private GameObject destroyEffectPrefab;
        [SerializeField] private GameObject destroyTarget;
        [SerializeField] private bool crashOnDeath = true;
        [SerializeField] private float crashFallSpeed = 25f;
        [SerializeField] private float crashDestroyDistance = 45f;
        [SerializeField] private float crashRollSpeed = 120f;
        [SerializeField] private bool disableCollidersOnDeath = true;
        [SerializeField] private float destroyEffectLifetime = 5f;

        public UnityEvent<float, float> damaged;
        public UnityEvent destroyed;

        private float currentHealth;
        private bool isDestroyed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthRatio => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        public bool IsAlive => !isDestroyed && currentHealth > 0f;

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
            isDestroyed = false;
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, null);
        }

        public void TakeDamage(float damage, GameObject source)
        {
            if (isDestroyed || damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            damaged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                DestroyEnemy();
            }
        }

        private void DestroyEnemy()
        {
            if (isDestroyed)
            {
                return;
            }

            isDestroyed = true;
            destroyed?.Invoke();

            if (destroyEffectPrefab != null)
            {
                var effect = Instantiate(destroyEffectPrefab, transform.position, transform.rotation);
                Destroy(effect, destroyEffectLifetime);
            }

            var target = destroyTarget != null ? destroyTarget : gameObject;
            if (crashOnDeath)
            {
                StartCoroutine(CrashAndDestroy(target));
                return;
            }

            Destroy(target);
        }

        private IEnumerator CrashAndDestroy(GameObject target)
        {
            if (target == null)
            {
                yield break;
            }

            var mover = target.GetComponent<EnemyStraightMover>();
            if (mover != null)
            {
                mover.enabled = false;
            }

            if (disableCollidersOnDeath)
            {
                foreach (var collider in target.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
            }

            var startY = target.transform.position.y;
            while (target != null && startY - target.transform.position.y < crashDestroyDistance)
            {
                target.transform.position += Vector3.down * crashFallSpeed * Time.deltaTime;
                target.transform.Rotate(Vector3.forward, crashRollSpeed * Time.deltaTime, Space.Self);
                yield return null;
            }

            if (target != null)
            {
                Destroy(target);
            }
        }
    }
}
