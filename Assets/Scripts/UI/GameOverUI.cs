using UnityEngine;
using JetSimulation.Core; // SceneController가 있는 네임스페이스

namespace JetSimulation.UI
{
    public sealed class GameOverUI : MonoBehaviour
    {
        [Tooltip("씬 매니저를 연결해주세요")]
        [SerializeField] private SceneController sceneController;

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