using System;
using JetSimulation.Combat;
using JetSimulation.EnemySystem;
using UnityEngine;

namespace JetSimulation.Core
{
    public sealed class PlayerHealth : MonoBehaviour, IAttackDamageReceiver
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Collision Effect")]
        [SerializeField] private GameObject collisionEffectPrefab;
        [SerializeField, Min(0f)] private float collisionEffectLifetime = 5f;
        [SerializeField] private bool alignCollisionEffectToContactNormal = true;

        private float currentHealth;

        public bool IsDead { get; private set; }

        public event Action<float> OnHealthChanged;
        public event Action OnTookDamage;
        public event Action OnPlayerDied;

        private void Awake()
        {
            currentHealth = maxHealth;
            IsDead = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var hitPoint = other.ClosestPoint(transform.position);
            if (!IsFinite(hitPoint))
            {
                hitPoint = transform.position;
            }

            TryHandleEnemyCollision(other.gameObject, hitPoint, GetDirectionFromSource(other.gameObject));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            var hitPoint = transform.position;
            var hitNormal = GetDirectionFromSource(collision.gameObject);

            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                hitPoint = contact.point;
                hitNormal = contact.normal;
            }

            TryHandleEnemyCollision(collision.gameObject, hitPoint, hitNormal);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            var actualDamage = Mathf.Min(amount, currentHealth);
            currentHealth = Mathf.Max(0f, currentHealth - amount);

            OnHealthChanged?.Invoke(currentHealth / maxHealth);
            OnTookDamage?.Invoke();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.DeductScoreForDamage(Mathf.RoundToInt(actualDamage * 2));
            }

            Debug.Log($"[Player] Took damage: {amount} | Remaining health: {currentHealth}");

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void KillInstantly(GameObject source)
        {
            if (IsDead)
            {
                return;
            }

            currentHealth = 0f;
            OnHealthChanged?.Invoke(0f);
            Debug.Log($"[Player] Instant collision death: {(source != null ? source.name : "Unknown")}");
            Die();
        }

        private void TryHandleEnemyCollision(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead || hitObject == null)
            {
                return;
            }

            if (hitObject.GetComponentInParent<EnemyHealth>() == null &&
                hitObject.GetComponentInParent<EnemyController>() == null &&
                hitObject.GetComponentInParent<EnemyBossController>() == null)
            {
                return;
            }

            PlayCollisionEffect(hitPoint, hitNormal);
            KillInstantly(hitObject);
        }

        private void PlayCollisionEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (collisionEffectPrefab == null)
            {
                return;
            }

            if (!IsFinite(hitPoint))
            {
                hitPoint = transform.position;
            }

            var rotation = transform.rotation;
            if (alignCollisionEffectToContactNormal && hitNormal.sqrMagnitude > Mathf.Epsilon)
            {
                rotation = Quaternion.LookRotation(hitNormal.normalized, Vector3.up);
            }

            var effect = Instantiate(collisionEffectPrefab, hitPoint, rotation);
            if (collisionEffectLifetime > 0f)
            {
                Destroy(effect, collisionEffectLifetime);
            }
        }

        private Vector3 GetDirectionFromSource(GameObject source)
        {
            if (source == null)
            {
                return transform.forward;
            }

            var direction = transform.position - source.transform.position;
            return direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : transform.forward;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void Die()
        {
            IsDead = true;
            OnPlayerDied?.Invoke();

            Debug.Log("[Player] Entered dead state.");
        }
    }
}
