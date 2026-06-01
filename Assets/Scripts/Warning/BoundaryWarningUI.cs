using UnityEngine;
using TMPro;
using JetSimulation.Environment;

namespace JetSimulation.UI
{
    public sealed class BoundaryWarningUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FlightBoundaryManager boundaryManager;
        [SerializeField] private TextMeshProUGUI warningText;
        // warningPanel 변수는 완전히 삭제했습니다.

        [Header("Settings")]
        [Tooltip("텍스트가 깜빡이는 속도")]
        [SerializeField] private float blinkSpeed = 5f;

        private bool isWarningActive = false;

        private void OnEnable()
        {
            if (boundaryManager != null)
            {
                boundaryManager.OnBoundaryStateChanged += HandleWarningState;
                boundaryManager.OnWarningTimerUpdated += UpdateTimerText;
            }

            // 시작할 땐 텍스트 숨기기
            if (warningText != null) warningText.enabled = false;
        }

        private void OnDisable()
        {
            if (boundaryManager != null)
            {
                boundaryManager.OnBoundaryStateChanged -= HandleWarningState;
                boundaryManager.OnWarningTimerUpdated -= UpdateTimerText;
            }
        }

        private void HandleWarningState(bool showWarning)
        {
            isWarningActive = showWarning;

            if (warningText != null)
            {
                warningText.enabled = showWarning; // 텍스트 컴포넌트 자체를 켜고 끕니다.

                // 꺼질 때 알파(투명도)값을 원래대로 복구해둠
                if (!showWarning)
                {
                    Color c = warningText.color;
                    c.a = 1f;
                    warningText.color = c;
                }
            }
        }

        private void UpdateTimerText(float timeRemaining)
        {
            if (warningText != null && isWarningActive)
            {
                warningText.text = $"경고: 작전 구역 이탈!\n<color=red>{timeRemaining:F2}</color>초 뒤 폭파";
            }
        }

        private void Update()
        {
            // 경고 중일 때만 텍스트 알파값을 핑퐁(PingPong)시켜 깜빡임 효과 연출
            if (isWarningActive && warningText != null)
            {
                Color c = warningText.color;
                // 완전히 안 보이면 글자를 읽기 힘드므로 0.3 ~ 1.0 사이에서 깜빡이게 설정
                c.a = Mathf.Lerp(0.3f, 1f, Mathf.PingPong(Time.time * blinkSpeed, 1f));
                warningText.color = c;
            }
        }
    }
}