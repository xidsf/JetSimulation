using UnityEngine;
using JetSimulation.Combat;

namespace JetSimulation.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissileAttackController playerAttackController;
        // [SerializeField] private PlayerJetController playerFlightController; // 추후 조종 스크립트 연동

        private void OnEnable()
        {
            // PlayerHealth의 사망 이벤트 구독
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied += HandleGameOver;
            }
        }

        private void OnDisable()
        {
            // 메모리 누수 방지를 위한 구독 해제
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied -= HandleGameOver;
            }
        }

        private void HandleGameOver()
        {
            Debug.Log("게임 오버! 조작을 차단합니다.");

            // 1. 공격 조작 차단 (팀원 스크립트 활용)
            if (playerAttackController != null)
            {
                playerAttackController.SetAttackEnabled(false);
            }

            // 2. 비행 조작 차단 (스크립트 활성화 상태 끄기)
            // if (playerFlightController != null)
            // {
            //     playerFlightController.enabled = false; 
            // }

            // 3. UIManager를 통한 게임 오버 화면 출력 (다음 단계에서 연동)
            // UIManager.Instance.ShowPanel(UIType.GameOver);
        }
    }
}