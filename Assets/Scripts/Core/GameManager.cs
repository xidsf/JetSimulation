using UnityEngine;
using System;
using System.Collections;
using JetSimulation.Combat;
using JetSimulation.EnemySystem;
using JetSimulation.UI;

namespace JetSimulation.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissileAttackController playerAttackController;
        [SerializeField] private PlayerMachineGunController playerMachineGunController;
        [SerializeField] private PlayerWeaponController legacyPlayerWeaponController;
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

        [Header("VR Transition (Boss Clear)")]
        [Tooltip("보스 클리어 시 암전 연출에 사용할 FadeSphere의 Renderer를 연결해주세요.")]
        [SerializeField] private Renderer fadeRenderer;
        [Tooltip("암전에 걸리는 시간 (사망 연출과 동일하게 맞추는 것을 추천합니다)")]
        [SerializeField] private float fadeDuration = 2f;

        public event Action<int> OnScoreChanged;
        public event Action OnGameOver;

        public int CurrentScore => currentScore;
        public bool IsGameOver { get; private set; }

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
            IsGameOver = false;
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
            if (IsGameOver)
            {
                return;
            }

            Debug.Log("게임 오버! 모든 조작 및 점수 기록을 차단합니다.");

            IsGameOver = true;
            isScoreActive = false;
            StopAllCoroutines();
            OnGameOver?.Invoke();

            if (playerAttackController != null)
            {
                playerAttackController.SetAttackEnabled(false);
                playerAttackController.enabled = false;
            }

            if (playerMachineGunController == null)
            {
                playerMachineGunController = FindObjectOfType<PlayerMachineGunController>();
            }

            if (playerMachineGunController != null)
            {
                playerMachineGunController.SetMachineGunEnabled(false);
                playerMachineGunController.enabled = false;
            }

            if (legacyPlayerWeaponController == null)
            {
                legacyPlayerWeaponController = FindObjectOfType<PlayerWeaponController>();
            }

            if (legacyPlayerWeaponController != null)
            {
                legacyPlayerWeaponController.SetWeaponEnabled(false);
                legacyPlayerWeaponController.enabled = false;
            }

            if (playerFlightController != null)
            {
                // 1. 관성 완전 차단 (기체 물리 정지)
                playerFlightController.HaltFlight();

                // 2. 바다 밑에 처박혔다면 즉시 수면 위 안전한 고도로 끌어올림 (UI 클리핑 원천 차단)
                if (playerFlightController.transform.position.y <= seaLevelY + 5f)
                {
                    Vector3 pos = playerFlightController.transform.position;
                    playerFlightController.transform.position = new Vector3(pos.x, seaLevelY + 20f, pos.z);
                }
            }

            StopEnemySystems();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanelType.GameOver);
            }
            else
            {
                Debug.LogWarning("[GameManager] UIManager 인스턴스가 없어 게임 오버 패널을 띄울 수 없습니다.");
            }
        }

        private void StopEnemySystems()
        {
            foreach (var spawner in FindObjectsOfType<EnemySpawner>())
            {
                spawner.StopSpawning();
            }

            foreach (var enemy in FindObjectsOfType<EnemyController>())
            {
                enemy.StopMovement();
            }

            foreach (var mover in FindObjectsOfType<EnemyStraightMover>())
            {
                mover.StopMovement();
            }

            foreach (var boss in FindObjectsOfType<EnemyBossController>())
            {
                boss.StopForGameOver();
            }

            foreach (var missile in FindObjectsOfType<EnemyBossMissile>())
            {
                missile.DestroyForGameOver();
            }

            foreach (var missile in FindObjectsOfType<BossStraightMissile>())
            {
                missile.DestroyForGameOver();
            }
        }

        /// <summary>
        /// 보스가 파괴되었을 때(클리어) 호출되는 메서드
        /// </summary>
        public void HandleBossCleared()
        {
            Debug.Log("보스 클리어! 점수 기록을 차단하고 종료 연출을 시작합니다.");

            isScoreActive = false;
            StopAllCoroutines();

            if (playerAttackController != null)
            {
                playerAttackController.SetAttackEnabled(false);
            }

            StartCoroutine(BossClearRoutine());
        }

        private IEnumerator BossClearRoutine()
        {
            // 1. 보스가 폭발하는 화려한 이펙트를 감상하기 위해 3초간 비행 유지
            yield return new WaitForSeconds(3f);

            // 2. 플레이어 사망 시와 동일하게 기체 물리 관성 및 조작 완전 차단
            if (playerFlightController != null)
            {
                playerFlightController.HaltFlight();
            }

            // 3. VR 환경 멀미 방지 및 연출을 위해 FadeSphere를 서서히 어둡게 (암전)
            if (fadeRenderer != null)
            {
                float timer = 0f;
                Color fadeColor = fadeRenderer.material.color;

                while (timer < fadeDuration)
                {
                    timer += Time.deltaTime;
                    fadeColor.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                    fadeRenderer.material.color = fadeColor;
                    yield return null;
                }
                fadeColor.a = 1f;
                fadeRenderer.material.color = fadeColor;
            }
            else
            {
                // 암전 렌더러가 할당되지 않았을 경우를 대비한 대기 시간
                yield return new WaitForSeconds(fadeDuration);
            }

            // 4. 시야가 완벽히 차단된 후 깔끔하게 UIManager를 통해 '작전 종료' 화면 호출
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanelType.GameOver);
            }
            else
            {
                Debug.LogWarning("[GameManager] UIManager 인스턴스가 없어 패널을 띄울 수 없습니다.");
            }
        }
    }
}
