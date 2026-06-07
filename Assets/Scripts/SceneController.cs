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
        [Tooltip("화면 암전에 사용할 FadeSphere의 Renderer를 연결해주세요. (인게임/시작화면 각각 배치 필요)")]
        [SerializeField] private Renderer fadeRenderer;
        [SerializeField] private float fadeDuration = 1.5f;

        private void Start()
        {
            // 씬 시작 시 화면이 완전히 보일 수 있도록 초기화 (알파 0)
            if (fadeRenderer != null)
            {
                SetFadeAlpha(0f);
            }
        }

        /// <summary>
        /// 시작(로비) 씬으로 비동기 이동합니다.
        /// </summary>
        public void LoadStartSceneAsync()
        {
            Debug.Log($"[{startSceneName}] 씬 비동기 로딩을 시작합니다.");
            StartCoroutine(LoadSceneRoutine(startSceneName));
        }

        /// <summary>
        /// 메인 게임 씬으로 비동기 이동합니다.
        /// </summary>
        public void LoadGameSceneAsync()
        {
            Debug.Log($"[{gameSceneName}] 씬 비동기 로딩을 시작합니다.");
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        /// <summary>
        /// 공통 비동기 로딩 및 암전 처리 루틴
        /// </summary>
        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            // 1. 부드럽게 화면 암전 (Fade Out)
            if (fadeRenderer != null)
                yield return StartCoroutine(FadeRoutine(0f, 1f));
            else
                yield return new WaitForSeconds(fadeDuration);

            // 2. 백그라운드에서 비동기 씬 로딩
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            // 유니티는 내부적으로 90%(0.9)까지 로딩되면 완료된 것으로 판단함
            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            // 3. 완전히 암전된 상태에서 안전하게 씬 전환 실행
            asyncLoad.allowSceneActivation = true;
        }

        private IEnumerator FadeRoutine(float startAlpha, float endAlpha)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
                SetFadeAlpha(currentAlpha);
                yield return null;
            }
            SetFadeAlpha(endAlpha);
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

        /// <summary>
        /// 게임을 완전히 종료합니다.
        /// </summary>
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