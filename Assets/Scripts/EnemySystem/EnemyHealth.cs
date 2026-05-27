using UnityEngine;
using UnityEngine.Events;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, Damageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private GameObject destroyEffectPrefab;
        [SerializeField] private GameObject destroyTarget;

        public UnityEvent<float, float> damaged;
        public UnityEvent destroyed;

        private float currentHealth;
        private bool isDestroyed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthRatio => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

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
                Instantiate(destroyEffectPrefab, transform.position, transform.rotation);
            }

            Destroy(destroyTarget != null ? destroyTarget : gameObject);
        }
    }
}
