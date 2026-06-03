using UnityEngine;
using System;

namespace JetSimulation.Core
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("플레이어의 최대 체력")]
        [SerializeField] private float maxHealth = 100f;

        private float currentHealth;

        public bool IsDead { get; private set; } = false;

        // 인자를 1개만 보내도록 원상 복구 (현재 체력)
        public event Action<float> OnHealthChanged;

        public event Action OnTookDamage;
        public event Action OnPlayerDied;

        private void Start()
        {
            currentHealth = maxHealth;
            IsDead = false;

            // 수정: 비율(0~1)로 넘겨주기
            OnHealthChanged?.Invoke(currentHealth / maxHealth);
        }
        public void TakeDamage(float damage, GameObject damageDealer = null)
        {
            if (IsDead || damage <= 0f) return;

            currentHealth -= damage;

            // 수정: 비율(0~1)로 넘겨주기
            OnHealthChanged?.Invoke(currentHealth / maxHealth);
            OnTookDamage?.Invoke();

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                IsDead = true;

                // 수정: 비율(0~1)로 넘겨주기
                OnHealthChanged?.Invoke(currentHealth / maxHealth);
                Debug.Log("[PlayerHealth] 플레이어 체력 0 도달 -> 사망 처리");

                OnPlayerDied?.Invoke();
            }
        }
    }
}