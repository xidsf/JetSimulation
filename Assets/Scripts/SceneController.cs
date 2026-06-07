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
        [SerializeField] private float fadeDuration = 1.0f; // 암전에 걸리는 시간

        private void Start()
        {
            // 씬 시작 시 화면이 완전히 보일 수 있도록 초기화
            SetFadeAlpha(0f);
        }

        /// <summary>
        /// 시작(로비) 씬으로 이동합니다.
        /// </summary>
        public void LoadStartScene()
        {
            StartCoroutine(TransitionAndLoad(startSceneName));
        }

        /// <summary>
        /// 메인 게임 씬으로 이동합니다.
        /// </summary>
        public void LoadGameScene()
        {
            StartCoroutine(TransitionAndLoad(gameSceneName));
        }

        private IEnumerator TransitionAndLoad(string sceneName)
        {
            // 1. 씬을 넘기기 전, 화면을 먼저 완전히 까맣게 만듭니다.
            if (fadeRenderer != null)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    timer += Time.deltaTime;
                    SetFadeAlpha(timer / fadeDuration);
                    yield return null;
                }
                SetFadeAlpha(1f); // 완벽한 암전 보장
            }

            // 2. 화면이 새카매진 상태에서 씬을 동기식으로 로드합니다.
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