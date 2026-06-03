using UnityEngine;
using UnityEngine.SceneManagement;

namespace JetSimulation.Core
{
    public sealed class SceneController : MonoBehaviour
    {
        [Header("Scene Names")]
        [Tooltip("이동할 씬의 이름을 정확히 입력해주세요 (오타 주의!)")]
        [SerializeField] private string startSceneName = "StartScene";
        [SerializeField] private string gameSceneName = "GameScene";

        /// <summary>
        /// 시작(로비) 씬으로 이동합니다.
        /// </summary>
        public void LoadStartScene()
        {
            Debug.Log($"[{startSceneName}] 씬으로 이동합니다.");
            SceneManager.LoadScene(startSceneName);
        }

        /// <summary>
        /// 메인 게임 씬으로 이동합니다.
        /// </summary>
        public void LoadGameScene()
        {
            Debug.Log($"[{gameSceneName}] 씬으로 이동합니다.");
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>
        /// 게임을 완전히 종료합니다.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("게임을 종료합니다.");

            // 유니티 에디터 환경에서 플레이 모드를 끄는 코드
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            // 실제 빌드된 파일(exe, apk 등)에서 게임을 끄는 코드
#else
            Application.Quit();
#endif
        }
    }
}