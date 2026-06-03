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

        [Header("Settings")]
        [Tooltip("텍스트가 깜빡이는 속도")]
        [SerializeField] private float blinkSpeed = 5f;

        private void OnEnable()
        {
            if (boundaryManager != null)
            {
                boundaryManager.OnWarningTimerUpdated += UpdateTimerText;
            }

            if (warningText != null)
            {
                Color c = warningText.color;
                c.a = 1f;
                warningText.color = c;
            }
        }

        private void OnDisable()
        {
            if (boundaryManager != null)
            {
                boundaryManager.OnWarningTimerUpdated -= UpdateTimerText;
            }
        }

        private void UpdateTimerText(float timeRemaining)
        {
            if (warningText != null)
            {
                warningText.text =
                    $"경고: 작전 구역 이탈!\n<color=red>{timeRemaining:F2}</color>초 뒤 폭파";
            }
        }

        private void Update()
        {
            if (warningText == null)
                return;

            Color c = warningText.color;
            c.a = Mathf.Lerp(
                0.3f,
                1f,
                Mathf.PingPong(Time.time * blinkSpeed, 1f)
            );

            warningText.color = c;
        }
    }
}