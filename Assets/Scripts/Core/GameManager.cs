using UnityEngine;
using System;
using System.Collections;
using JetSimulation.Combat;
using JetSimulation.UI;

namespace JetSimulation.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissileAttackController playerAttackController;
        [SerializeField] private PlayerJetController playerFlightController;

        [Header("Game State")]
        private int currentScore = 0;
        private bool isScoreActive = false;

        [Header("Score Settings")]
        [SerializeField] private int scorePerSecond = 10;
        [SerializeField] private float scoreTickInterval = 1f;

        [Header("Environment Settings")]
        [Tooltip("추락사로 판정되는 바다 수면 높이")]
        [SerializeField] private float seaLevelY = -1000f;

        public event Action<int> OnScoreChanged;

        public int CurrentScore => currentScore;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            StartGame();
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnPlayerDied += HandleGameOver;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnPlayerDied -= HandleGameOver;
        }

        public void StartGame()
        {
            currentScore = 0;
            isScoreActive = true;
            OnScoreChanged?.Invoke(currentScore);
            StartCoroutine(SurvivalScoreRoutine());
        }

        public void AddScoreForKill(int amount)
        {
            if (!isScoreActive || amount <= 0) return;
            currentScore += amount;
            OnScoreChanged?.Invoke(currentScore);
        }

        public void DeductScoreForDamage(int amount)
        {
            if (!isScoreActive || amount <= 0) return;
            currentScore -= amount;
            if (currentScore < 0) currentScore = 0;
            OnScoreChanged?.Invoke(currentScore);
        }

        private IEnumerator SurvivalScoreRoutine()
        {
            while (isScoreActive)
            {
                yield return new WaitForSeconds(scoreTickInterval);
                if (!isScoreActive) break;
                currentScore += scorePerSecond;
                OnScoreChanged?.Invoke(currentScore);
            }
        }

        private void HandleGameOver()
        {
            Debug.Log("게임 오버! 모든 조작 및 점수 기록을 차단합니다.");

            isScoreActive = false;
            StopAllCoroutines();

            if (playerAttackController != null)
            {
                playerAttackController.SetAttackEnabled(false);
            }

            if (playerFlightController != null)
            {
                // 1. 관성 완전 차단 (기체 물리 정지)
                playerFlightController.HaltFlight();

                // 2. 바다 밑에 처박혔다면 즉시 수면 위 안전한 고도로 끌어올림 (UI 클리핑 원천 차단)
                if (playerFlightController.transform.position.y <= seaLevelY + 5f)
                {
                    Vector3 pos = playerFlightController.transform.position;
                    // 수면(-1000)보다 20만큼 높은 -980으로 위치 강제 이동
                    playerFlightController.transform.position = new Vector3(pos.x, seaLevelY + 20f, pos.z);
                }
            }

            // UI 호출은 기존처럼 CameraEffectManager의 연출 로직에 맡깁니다.
        }
    }
}