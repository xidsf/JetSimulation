using UnityEngine;
using JetSimulation.Core; // GameManager 접근을 위해 추가

namespace JetSimulation.Combat
{
    public sealed class DemoDamageTarget : MonoBehaviour, IAttackDamageReceiver
    {
        [SerializeField] float health = 100f;
        [SerializeField] bool destroyWhenDead = false;

        [Header("Score Settings")]
        [Tooltip("이 객체가 파괴될 때 획득할 점수")]
        [SerializeField] int scoreOnDestroy = 500;

        private bool isDead = false; // 중복 파괴(점수 중복 획득) 방지 플래그

        public float Health => health;

        public void TakeDamage(float amount, GameObject source)
        {
            if (isDead) return; // 이미 죽은 객체면 데미지 무시

            health -= amount;
            Debug.Log($"{name} took {amount} damage from {source.name}. Remaining health: {health}", this);

            if (health <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            isDead = true; // 사망 처리

            // 게임 매니저에 격추 신호를 보내 점수 획득!
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScoreForKill(scoreOnDestroy);
            }

            if (destroyWhenDead)
            {
                Destroy(gameObject); // 객체 파괴
            }
        }
    }
}