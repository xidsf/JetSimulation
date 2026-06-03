using UnityEngine;
using System;
using System.Collections; // 코루틴 사용
using JetSimulation.Combat;

namespace JetSimulation.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissileAttackController playerAttackController;
        // [SerializeField] private PlayerJetController playerFlightController;

        [Header("Game State")]
        private int currentScore = 0;
        private bool isScoreActive = false; // 사망 시 점수 증감 차단용 플래그

        public int CurrentScore => currentScore;

        [Header("Score Settings")]
        [SerializeField] private int scorePerSecond = 10; // 생존 시 초당 획득 점수
        [SerializeField] private float scoreTickInterval = 1f; // 획득 주기 (초)



        public event Action<int> OnScoreChanged;

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
            // 게임 시작과 동시에 점수 시스템 가동
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

        // ==========================================
        // [게임 사이클 관리]
        // ==========================================

        public void StartGame()
        {
            currentScore = 0;
            isScoreActive = true;
            OnScoreChanged?.Invoke(currentScore);

            // 생존 시간에 따른 점수 증가 코루틴 시작
            StartCoroutine(SurvivalScoreRoutine());
        }

        // ==========================================
        // [점수 관리 시스템 (팀원 참조용 Public API)]
        // ==========================================

        /// <summary>
        /// 적 처치 시 점수 획득 (팀원 호출용)
        /// 사용법: GameManager.Instance.AddScoreForKill(100);
        /// </summary>
        public void AddScoreForKill(int amount)
        {
            if (!isScoreActive || amount <= 0) return;

            currentScore += amount;
            OnScoreChanged?.Invoke(currentScore);
            Debug.Log($"[적 격추] 점수 획득! 현재 점수: {currentScore}");
        }

        /// <summary>
        /// 플레이어 피격 시 점수 차감
        /// 사용법: GameManager.Instance.DeductScoreForDamage(50);
        /// </summary>
        public void DeductScoreForDamage(int amount)
        {
            if (!isScoreActive || amount <= 0) return;

            currentScore -= amount;

            // 점수가 마이너스가 되는 것을 방어
            if (currentScore < 0) currentScore = 0;

            OnScoreChanged?.Invoke(currentScore);
            Debug.Log($"[플레이어 피격] 점수 차감! 현재 점수: {currentScore}");
        }

        /// <summary>
        /// 생존 시간에 따른 자동 점수 증가 코루틴
        /// </summary>
        private IEnumerator SurvivalScoreRoutine()
        {
            while (isScoreActive)
            {
                yield return new WaitForSeconds(scoreTickInterval);

                // 대기 시간 도중 사망했을 경우를 대비한 2중 체크
                if (!isScoreActive) break;

                currentScore += scorePerSecond;
                OnScoreChanged?.Invoke(currentScore);
            }
        }

        // ==========================================

        private void HandleGameOver()
        {
            Debug.Log("게임 오버! 조작 및 점수 기록을 차단합니다.");

            // 1. 점수 기록 완전 중단
            isScoreActive = false;
            StopAllCoroutines(); // 생존 시간 점수 코루틴 즉시 정지

            // 2. 공격 및 조작 차단
            if (playerAttackController != null)
            {
                playerAttackController.SetAttackEnabled(false);
            }

            // if (playerFlightController != null) ...

            // 3. UIManager 연동 (GameOver 캔버스 출력)
            // UIManager.Instance.ShowPanel(UIType.GameOver);
        }
    }
}