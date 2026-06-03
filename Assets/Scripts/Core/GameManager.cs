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

        public event Action<int> OnScoreChanged;

        public int CurrentScore => currentScore;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartGame();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied += HandleGameOver;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied -= HandleGameOver;
            }
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

            // 1. 점수 기록 완전 중단
            isScoreActive = false;
            StopAllCoroutines();

            // 2. 공격 조작 차단 (팀원 스크립트 활성화 해제)
            if (playerAttackController != null)
            {
                playerAttackController.SetAttackEnabled(false);
            }

            // 3. 비행 조작 차단 주석 해제 (전진 및 회전 완전 정지)
            if (playerFlightController != null)
            {
                playerFlightController.enabled = false;
            }

            // 4. UIManager 연동 주석 해제 (GameOver 캔버스 출력)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanelType.GameOver);
            }
        }
    }
}