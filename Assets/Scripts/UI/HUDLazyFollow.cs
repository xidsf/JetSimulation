using UnityEngine;

namespace JetSimulation.UI
{
    public sealed class HUDLazyFollow : MonoBehaviour
    {
        [Header("Tracking Settings")]
        [Tooltip("따라갈 VR 카메라 (비워두면 Main Camera를 자동 할당합니다)")]
        [SerializeField] private Transform targetCamera;

        [Tooltip("카메라 정면으로부터 캔버스가 떨어져 있을 거리")]
        [SerializeField] private float distance = 1.5f;

        [Tooltip("따라오는 속도. 값이 낮을수록 묵직하고 부드럽게 따라옵니다.")]
        [SerializeField] private float followSpeed = 8f;

        [Tooltip("위치 미세 조정 (예: 시야보다 살짝 아래로 내리고 싶을 때 Y값을 조절)")]
        [SerializeField] private Vector3 offset = Vector3.zero;

        private void Start()
        {
            // 타겟 카메라가 지정되지 않았다면 씬의 메인 카메라를 자동으로 찾습니다.
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }
        }

        // Update가 아닌 LateUpdate를 사용하여 카메라 이동이 모두 끝난 후 UI가 따라가도록 처리 (떨림 방지)
        private void LateUpdate()
        {
            if (targetCamera == null) return;

            // 목표 위치 계산
            Vector3 targetPosition = targetCamera.position
                                   + (targetCamera.forward * distance)
                                   + (targetCamera.right * offset.x)
                                   + (targetCamera.up * offset.y);

            // Lerp(부드러운 이동)를 제거하고 위치와 회전을 즉각적으로 꽂아넣습니다.
            transform.position = targetPosition;
            transform.rotation = targetCamera.rotation;
        }
    }
}