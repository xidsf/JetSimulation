using UnityEngine;
using System;
using JetSimulation.Core;
using JetSimulation.UI;

namespace JetSimulation.Environment
{
    public sealed class FlightBoundaryManager : MonoBehaviour
    {
        [Header("Boundary Settings (Cylinder)")]
        [Tooltip("작전 구역의 최대 반경 (수평 이동 제한)")]
        [SerializeField] private float maxRadius = 2000f;
        [Tooltip("이탈 허용 시간 (초)")]
        [SerializeField] private float warningTimeLimit = 5f;

        [Header("Altitude Settings")]
        [Tooltip("추락사로 판정되는 바다의 Y 좌표 높이")]
        [SerializeField] private float seaLevelY = -1000f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform boundaryVisualizer;

        public event Action<bool> OnBoundaryStateChanged;
        public event Action<float> OnWarningTimerUpdated;

        private bool isOutOfBounds = false;
        private float currentWarningTime = 0f;

        private void Start()
        {
            // 시작 시 인스펙터에 설정된 반경으로 시각화 오브젝트 크기 초기화
            SetBoundaryRadius(maxRadius);
        }

        private void Update()
        {
            if (playerTransform == null || playerHealth == null || playerHealth.IsDead) return;

            CheckAltitudeDeath();
            CheckHorizontalBoundary();
        }

        // 1. 고도 확인 (추락사 판정)
        private void CheckAltitudeDeath()
        {
            if (playerTransform.position.y <= seaLevelY)
            {
                // 바다에 닿으면 즉시 9999 데미지 전달
                playerHealth.TakeDamage(9999f, gameObject);
            }
        }

        // 2. 수평 작전 구역 확인 (원기둥 판정)
        private void CheckHorizontalBoundary()
        {
            // Y축(고도)을 무시하고 X, Z 거리만 계산
            Vector2 playerPosXZ = new Vector2(playerTransform.position.x, playerTransform.position.z);
            float currentRadius = playerPosXZ.magnitude;

            bool currentlyOutOfBounds = currentRadius > maxRadius;

            if (currentlyOutOfBounds && !isOutOfBounds)
            {
                isOutOfBounds = true;
                currentWarningTime = warningTimeLimit;
                OnBoundaryStateChanged?.Invoke(true);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.EnablePanelOverlay(UIPanelType.Warning, true);
                }
            }
            else if (!currentlyOutOfBounds && isOutOfBounds)
            {
                isOutOfBounds = false;
                OnBoundaryStateChanged?.Invoke(false);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.EnablePanelOverlay(UIPanelType.Warning, false);
                }
            }

            // 이탈 중일 때 카운트다운 및 데미지 처리
            if (isOutOfBounds)
            {
                currentWarningTime -= Time.deltaTime;
                OnWarningTimerUpdated?.Invoke(Mathf.Max(0, currentWarningTime));

                if (currentWarningTime <= 0f)
                {
                    currentWarningTime = 0f;
                    playerHealth.TakeDamage(9999f, gameObject);
                }
            }
        }

        public void SetBoundaryRadius(float newRadius)
        {
            maxRadius = newRadius;
            if (boundaryVisualizer != null)
            {
                // Y축 크기는 맵의 천장 높이만큼 충분히 크게 늘려줌 (예: 5000)
                // X, Z축 크기만 maxRadius의 2배(지름)로 설정
                boundaryVisualizer.localScale = new Vector3(maxRadius * 2f, 5000f, maxRadius * 2f);
            }
        }
    }
}