using UnityEngine;
using System;
using JetSimulation.Combat; // 팀원의 Combat 인터페이스 참조를 위해 추가
using JetSimulation.EnemySystem;

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

        private void OnTriggerEnter(Collider other)
        {
            TryHandleEnemyCollision(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHandleEnemyCollision(collision.gameObject);
        }

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
            if (IsDead || amount <= 0f) return;

            // 핵심 수정: 오버킬(9999 대미지)이 들어와도 내 현재 체력 이상으로는 페널티를 받지 않음
            float actualDamage = Mathf.Min(amount, currentHealth);

            currentHealth -= amount;
            if (currentHealth < 0f) currentHealth = 0f;

            OnHealthChanged?.Invoke(currentHealth / maxHealth);
            OnTookDamage?.Invoke();

            if (GameManager.Instance != null)
            {
                // 실제 깎인 체력 비례로만 점수 차감 (즉사해도 최대 200점만 깎임)
                GameManager.Instance.DeductScoreForDamage(Mathf.RoundToInt(actualDamage * 2));
            }

            Debug.Log($"[Player] 피격! 대미지: {amount} | 남은 체력: {currentHealth}");

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
            Debug.Log($"[Player] 즉사 충돌 발생: {(source != null ? source.name : "Unknown")}");
            Die();
        }

        private void TryHandleEnemyCollision(GameObject hitObject)
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

            KillInstantly(hitObject);
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
