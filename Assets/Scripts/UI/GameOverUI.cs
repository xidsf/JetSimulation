using UnityEngine;
using TMPro;
using JetSimulation.Core;

namespace JetSimulation.UI
{
    public sealed class GameOverUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("씬 매니저를 연결해주세요")]
        [SerializeField] private SceneController sceneController;

        [Tooltip("최종 점수를 띄울 TextMeshPro 컴포넌트를 연결해주세요")]
        [SerializeField] private TextMeshProUGUI finalScoreText;

        // UIManager가 이 UI를 켤 때(SetActive(true)) 자동으로 실행됨
        private void OnEnable()
        {
            UpdateFinalScoreUI();
        }

        private void UpdateFinalScoreUI()
        {
            if (GameManager.Instance != null && finalScoreText != null)
            {
                // 천 단위 콤마 포맷(N0)을 적용하여 깔끔하게 출력
                finalScoreText.text = $"최종 점수: {GameManager.Instance.CurrentScore:N0}";
            }
        }

        /// <summary>
        /// 다시 시작하기 버튼 클릭 이벤트
        /// </summary>
        public void OnClickRetry()
        {
            // VR 프레임 드랍 및 멀미 방지를 위해 비동기 메서드로 변경
            if (sceneController != null)
            {
                sceneController.LoadGameSceneAsync();
            }
        }

        /// <summary>
        /// 로비로 돌아가기 버튼 클릭 이벤트
        /// </summary>
        public void OnClickLobby()
        {
            // 시작 화면으로 돌아갈 때도 안전하게 비동기 암전 로딩 적용
            if (sceneController != null)
            {
                sceneController.LoadStartSceneAsync();
            }
        }
    }
}