using UnityEngine;
using TMPro; // TextMeshPro 사용
using JetSimulation.Core;

namespace JetSimulation.UI
{
    public sealed class ScoreHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("실시간 점수를 표시할 TextMeshPro 컴포넌트를 연결해주세요")]
        [SerializeField] private TextMeshProUGUI scoreText;

        private bool isSubscribed = false;

        // UIManager에 의해 HUD 패널이 켜지거나, 씬이 시작될 때 실행
        private void OnEnable()
        {
            TrySubscribe();
        }

        // Awake/OnEnable 이후 첫 프레임에 실행 (초기화 타이밍 안전장치)
        private void Start()
        {
            TrySubscribe();
        }

        // HUD 패널이 꺼지거나 오브젝트가 비활성화될 때 실행
        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// GameManager의 점수 변동 이벤트를 안전하게 구독합니다.
        /// </summary>
        private void TrySubscribe()
        {
            if (isSubscribed) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged += UpdateScoreUI;

                // 구독하는 순간의 최신 점수로 텍스트 초기화 (예: 0점)
                UpdateScoreUI(GameManager.Instance.CurrentScore);
                isSubscribed = true;
            }
        }

        /// <summary>
        /// 메모리 누수 방지를 위해 구독을 해제합니다.
        /// </summary>
        private void Unsubscribe()
        {
            if (!isSubscribed) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged -= UpdateScoreUI;
            }
            isSubscribed = false;
        }

        /// <summary>
        /// GameManager로부터 이벤트 신호를 받아 실시간으로 UI를 갱신합니다.
        /// </summary>
        private void UpdateScoreUI(int currentScore)
        {
            if (scoreText != null)
            {
                // 천 단위 콤마 포맷(N0)을 적용하여 가독성을 높임 (예: SCORE: 1,250)
                scoreText.text = $"점수: {currentScore:N0}";
            }
        }
    }
}