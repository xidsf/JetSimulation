using UnityEngine;
using UnityEngine.InputSystem; // 최신 인풋 시스템 사용을 위해 추가
using JetSimulation.Core;
using JetSimulation.UI;
using JetSimulation.Environment;

namespace JetSimulation.DebugUtils
{
    public sealed class SimulationDebugger : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Target Managers")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private FlightBoundaryManager boundaryManager;

        [Header("Debug Settings")]
        [Tooltip("테스트를 위해 축소할 임시 맵 반경")]
        [SerializeField] private float debugBoundaryRadius = 50f;

        private void Update()
        {
            // 현재 연결된 키보드를 가져옵니다.
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 1. 일반 피격 테스트 (숫자 1키)
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(10f, gameObject);
                    Debug.Log("[Debug] 숫자 1키 입력: 플레이어 체력 10 감소");
                }
            }

            // 2. 즉사 및 게임 오버 테스트 (숫자 2키)
            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(9999f, gameObject);
                    Debug.Log("[Debug] 숫자 2키 입력: 플레이어 즉사 처리");
                }
            }

            // 3. 맵 이탈 테스트 - 반경 강제 축소 (숫자 3키)
            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                if (boundaryManager != null)
                {
                    //// 아까 만든 퍼블릭 함수를 깔끔하게 호출!
                    //boundaryManager.SetBoundaryRadius(debugBoundaryRadius);
                    Debug.Log($"[Debug] 숫자 3키 입력: 맵 한계선을 {debugBoundaryRadius}m로, 스케일을 {debugBoundaryRadius * 2f}로 좁혔습니다.");
                }
            }
        }
#endif
    }
}