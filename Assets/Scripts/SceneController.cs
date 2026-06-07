using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JetSimulation.Core
{
    public sealed class SceneController : MonoBehaviour
    {
        [Header("Scene Names")]
        [Tooltip("시작(로비) 씬의 이름을 정확히 입력해주세요.")]
        [SerializeField] private string startSceneName = "StartScene";

        [Tooltip("메인 게임 씬의 이름을 정확히 입력해주세요.")]
        [SerializeField] private string gameSceneName = "GameScene";

        [Header("VR Transition")]
        [Tooltip("화면 암전에 사용할 FadeSphere의 Renderer를 연결해주세요.")]
        [SerializeField] private Renderer fadeRenderer;

        [Tooltip("씬 전환 시 서서히 사라지게 할 UI 캔버스 그룹을 연결해주세요 (옵션)")]
        [SerializeField] private CanvasGroup uiCanvasGroup;

        [SerializeField] private float fadeDuration = 1.0f;

        private void Start()
        {
            // 씬 시작 시 화면이 완전히 보일 수 있도록 초기화
            SetFadeAlpha(0f);
        }

        public void LoadStartScene()
        {
            StartCoroutine(TransitionAndLoad(startSceneName));
        }

        public void LoadGameScene()
        {
            StartCoroutine(TransitionAndLoad(gameSceneName));
        }

        private IEnumerator TransitionAndLoad(string sceneName)
        {
            float timer = 0f;
            float startSphereAlpha = 0f;

            // 1. 현재 화면이 이미 얼마나 어두운 상태인지 체크
            if (fadeRenderer != null && fadeRenderer.material.HasProperty("_Color"))
            {
                startSphereAlpha = fadeRenderer.material.color.a;
            }

            // 이미 화면이 완전히 까만 상태인지 확인 (오차 허용)
            bool isAlreadyDark = startSphereAlpha >= 0.95f;

            // 2. 암전 및 UI 페이드 아웃 동시 진행
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;

                // 화면이 밝은 상태(시작 화면)라면 서서히 암전 진행
                if (fadeRenderer != null && !isAlreadyDark)
                {
                    SetFadeAlpha(Mathf.Lerp(startSphereAlpha, 1f, progress));
                }

                // UI 캔버스가 연결되어 있다면, 텍스트와 버튼을 서서히 투명하게(사라지게) 만듦
                if (uiCanvasGroup != null)
                {
                    uiCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
                }

                yield return null;
            }

            // 3. 최종 상태 확정 (완전 암전, UI 완전 투명)
            SetFadeAlpha(1f);
            if (uiCanvasGroup != null) uiCanvasGroup.alpha = 0f;

            // 4. 안전하게 씬 이동
            SceneManager.LoadScene(sceneName);
        }

        private void SetFadeAlpha(float alpha)
        {
            if (fadeRenderer != null && fadeRenderer.material.HasProperty("_Color"))
            {
                Color color = fadeRenderer.material.color;
                color.a = alpha;
                fadeRenderer.material.color = color;
            }
        }

        public void QuitGame()
        {
            Debug.Log("게임을 종료합니다.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}