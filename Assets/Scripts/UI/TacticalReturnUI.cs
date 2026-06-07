using UnityEngine;
using TMPro;

namespace JetSimulation.UI
{
    public class TacticalReturnUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("회전시킬 꺾쇠(^) TextMeshPro 컴포넌트")]
        [SerializeField]
        private TextMeshProUGUI indicatorText; // RectTransform에서 변경됨

        [Tooltip("남은 거리와 방위각을 띄워줄 TextMeshPro")]
        [SerializeField]
        private TextMeshProUGUI tacticalDataText;

        [Header("Targeting")]
        [Tooltip("플레이어 기체 (PlayerJet)")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("복귀해야 할 맵의 안전 구역 중심점")]
        [SerializeField] private Vector3 targetPosition = new Vector3(0f, 0f, 2250f);

        private void Start()
        {
            // 인스펙터가 아닌 코드 상에서 직접 꺾쇠 문자를 할당
            if (indicatorText != null)
            {
                indicatorText.text = "^";
            }
        }

        private void Update()
        {
            if (playerTransform == null || indicatorText == null || tacticalDataText == null) return;

            // 1. 방향 및 거리 계산
            Vector3 dirToTarget = targetPosition - playerTransform.position;
            dirToTarget.y = 0f;
            float distanceToTarget = dirToTarget.magnitude;
            dirToTarget.Normalize();

            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            Vector3 playerRight = playerTransform.right;
            playerRight.y = 0f;
            playerRight.Normalize();

            // 2. 방향 각도 계산 및 UI 회전
            float forwardDot = Vector3.Dot(playerForward, dirToTarget);
            float rightDot = Vector3.Dot(playerRight, dirToTarget);
            float angle = Mathf.Atan2(rightDot, forwardDot) * Mathf.Rad2Deg;

            // indicatorText의 rectTransform을 가져와서 회전 적용
            indicatorText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);

            // 3. 전술 데이터 텍스트 업데이트 (하이테크 스타일 적용)
            float absoluteHeading = Vector3.SignedAngle(Vector3.forward, dirToTarget, Vector3.up);
            if (absoluteHeading < 0) absoluteHeading += 360f;

            tacticalDataText.text = $"<color=red>[ LEAVING A.O. ]</color>\n" +
                                    $"TGT BRG: <mspace=0.6em>{absoluteHeading:000}</mspace>°\n" +
                                    $"TGT RNG: <mspace=0.6em>{distanceToTarget:N0}</mspace>";
        }
    }
}