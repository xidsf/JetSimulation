using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 추가
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
            // GameManager 싱글톤 인스턴스가 존재하고 텍스트 컴포넌트가 연결되어 있다면
            if (GameManager.Instance != null && finalScoreText != null)
            {
                // 천 단위 콤마 포맷(N0)을 적용하여 깔끔하게 출력
                finalScoreText.text = $"최종 점수: {GameManager.Instance.CurrentScore:N0}";
            }
        }

        public void OnClickRetry()
        {
            if (sceneController != null) sceneController.LoadGameScene();
        }

        public void OnClickLobby()
        {
            if (sceneController != null) sceneController.LoadStartScene();
        }
    }
}