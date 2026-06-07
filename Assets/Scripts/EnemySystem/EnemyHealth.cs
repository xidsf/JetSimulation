using System.Collections;
using System.Collections.Generic;
using JetSimulation.Core;
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
        [SerializeField] private int scoreOnDestroy = 100;

        public UnityEvent<float, float> damaged;
        public UnityEvent destroyed;

        public static readonly List<EnemyHealth> ActiveEnemies = new List<EnemyHealth>();

        private static int localDebugScore;

        private float currentHealth;
        private bool isDestroyed;
        private bool gameOverSubscribed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthRatio => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        public bool IsAlive => !isDestroyed && currentHealth > 0f;
        public static int LocalDebugScore => localDebugScore;

        private void Awake()
        {
            ResetHealth();
        }

        private void Start()
        {
            TrySubscribeGameOver();
        }

        private void OnEnable()
        {
            if (!ActiveEnemies.Contains(this))
            {
                ActiveEnemies.Add(this);
            }

            TrySubscribeGameOver();
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
            UnsubscribeGameOver();
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
            AddKillScore();

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

            var controller = target.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.enabled = false;
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

        private void AddKillScore()
        {
            if (scoreOnDestroy <= 0)
            {
                Debug.Log("[EnemySystem] Enemy destroyed, but scoreOnDestroy is 0 or lower.");
                return;
            }

            localDebugScore += scoreOnDestroy;

            if (GameManager.Instance == null)
            {
                Debug.Log($"[EnemySystem] Enemy destroyed. GameManager not found. Local debug score: {localDebugScore}");
                return;
            }

            var previousScore = GameManager.Instance.CurrentScore;
            GameManager.Instance.AddScoreForKill(scoreOnDestroy);

            var currentScore = GameManager.Instance.CurrentScore;
            if (currentScore == previousScore)
            {
                Debug.Log($"[EnemySystem] Enemy destroyed. Requested +{scoreOnDestroy}, but GameManager score stayed {currentScore}. Local debug score: {localDebugScore}");
                return;
            }

            Debug.Log($"[EnemySystem] Enemy destroyed. Score: {previousScore} -> {currentScore}");
        }

        private void HandleGameOver()
        {
            StopAllCoroutines();
        }

        private void TrySubscribeGameOver()
        {
            if (gameOverSubscribed || GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.OnGameOver += HandleGameOver;
            gameOverSubscribed = true;
        }

        private void UnsubscribeGameOver()
        {
            if (!gameOverSubscribed || GameManager.Instance == null)
            {
                gameOverSubscribed = false;
                return;
            }

            GameManager.Instance.OnGameOver -= HandleGameOver;
            gameOverSubscribed = false;
        }
    }
}
