using UnityEngine;
using System;
using JetSimulation.Combat; // 팀원의 Combat 인터페이스 참조를 위해 추가

namespace JetSimulation.Core
{
    public sealed class PlayerHealth : MonoBehaviour, IAttackDamageReceiver
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        public bool IsDead { get; private set; }

        // 기존 코어 아키텍처의 핵심 이벤트 Action들
        public event Action<float> OnHealthChanged; // 체력 비율 (0 ~ 1) 전달용
        public event Action OnTookDamage;
        public event Action OnPlayerDied;

        private void Awake()
        {
            currentHealth = maxHealth;
            IsDead = false;
        }

        /// <summary>
        /// 미사일 폭발/충돌 스크립트가 플레이어를 맞췄을 때 호출하게 될 인터페이스 메서드
        /// </summary>
        public void TakeDamage(float amount, GameObject source)
        {
            // 이미 죽었거나 데미지가 비정상적이면 무시
            if (IsDead || amount <= 0f) return;

            currentHealth -= amount;
            if (currentHealth < 0f) currentHealth = 0f;

            // 1. 실시간 HUD 체력 바 최신화를 위한 이벤트 발생
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            // 2. CameraEffectManager가 수신하여 화면을 붉게 번쩍이게 만드는 피격 이벤트 발생
            OnTookDamage?.Invoke();

            // 3. 피격 시 점수가 깎이는 기획 반영 (필요 시 수치 조정)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DeductScoreForDamage(Mathf.RoundToInt(amount * 2));
            }

            Debug.Log($"[Player] 피격! 대미지: {amount} | 남은 체력: {currentHealth}");

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            IsDead = true;

            // 4. GameManager와 CameraEffectManager가 수신하여 암전 및 조작을 끊는 사망 이벤트 발생
            OnPlayerDied?.Invoke();

            Debug.Log("[Player] 사망 상태에 진입하여 시네마틱 연출을 시작합니다.");
        }
    }
}