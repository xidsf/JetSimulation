using System;
using UnityEngine;
using JetSimulation.Combat;

namespace JetSimulation.Core
{
    public sealed class PlayerHealth : MonoBehaviour, IAttackDamageReceiver
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;
        private bool isDead;

        // UI와 시스템이 구독할 이벤트 목록
        public event Action<float> OnHealthChanged; // 0.0 ~ 1.0 비율 전달
        public event Action OnTookDamage;           // 피격 이펙트용 신호
        public event Action OnPlayerDied;           // 사망 신호

        private void Start()
        {
            currentHealth = maxHealth;
            // 시작 시 UI 게이지를 꽉 차게 초기화
            OnHealthChanged?.Invoke(1f);
        }

        // 팀원이 만든 IAttackDamageReceiver 인터페이스 구현
        public void TakeDamage(float amount, GameObject source)
        {
            if (isDead) return;

            currentHealth -= amount;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            // 구독중인 UI와 시스템에 이벤트 발송
            OnTookDamage?.Invoke();
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            isDead = true;
            OnPlayerDied?.Invoke();
        }
    }
}