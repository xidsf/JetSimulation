using UnityEngine;
using System;
using JetSimulation.Core;
using JetSimulation.UI;

namespace JetSimulation.Environment
{
    public sealed class FlightBoundaryManager : MonoBehaviour
    {
        [Header("Boundary Settings (Rectangular)")]
        [Tooltip("맵의 좌우(X축) 제한 범위 (예: -1000 ~ 1000)")]
        [SerializeField] private Vector2 limitX = new Vector2(-1000f, 1000f);
        [Tooltip("맵의 전후(Z축) 제한 범위 (예: 앞쪽으로 길게 -500 ~ 5000)")]
        [SerializeField] private Vector2 limitZ = new Vector2(-500f, 5000f);
        [Tooltip("맵의 최대 고도 (구름 천장 높이)")]
        [SerializeField] private float ceilingY = 2000f;
        [Tooltip("이탈 허용 시간 (초)")]
        [SerializeField] private float warningTimeLimit = 5f;

        [Header("Altitude Settings")]
        [Tooltip("추락사로 판정되는 바다의 Y 좌표 높이")]
        [SerializeField] private float seaLevelY = -1000f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerHealth playerHealth;

        public event Action<bool> OnBoundaryStateChanged;
        public event Action<float> OnWarningTimerUpdated;

        private bool isOutOfBounds = false;
        private float currentWarningTime = 0f;

        private void Update()
        {
            if (playerTransform == null || playerHealth == null || playerHealth.IsDead) return;

            CheckAltitudeDeath();
            CheckRectangularBoundary();
        }

        private void CheckAltitudeDeath()
        {
            if (playerTransform.position.y <= seaLevelY)
            {
                playerHealth.TakeDamage(9999f, gameObject);
            }
        }

        private void CheckRectangularBoundary()
        {
            Vector3 pos = playerTransform.position;

            // X(좌우), Z(전후), Y(천장) 중 하나라도 범위를 벗어났는지 확인
            bool currentlyOutOfBounds = pos.x < limitX.x || pos.x > limitX.y ||
                                        pos.z < limitZ.x || pos.z > limitZ.y ||
                                        pos.y > ceilingY;

            if (currentlyOutOfBounds && !isOutOfBounds)
            {
                isOutOfBounds = true;
                currentWarningTime = warningTimeLimit;
                OnBoundaryStateChanged?.Invoke(true);

                if (UIManager.Instance != null) UIManager.Instance.EnablePanelOverlay(UIPanelType.Warning, true);
            }
            else if (!currentlyOutOfBounds && isOutOfBounds)
            {
                isOutOfBounds = false;
                OnBoundaryStateChanged?.Invoke(false);

                if (UIManager.Instance != null) UIManager.Instance.EnablePanelOverlay(UIPanelType.Warning, false);
            }

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

        // 유니티 씬(Scene) 뷰에서 맵의 크기를 노란색 선으로 그려주는 보조 기능
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            float centerX = (limitX.x + limitX.y) / 2f;
            float centerZ = (limitZ.x + limitZ.y) / 2f;
            float centerY = (ceilingY + seaLevelY) / 2f;

            Vector3 center = new Vector3(centerX, centerY, centerZ);
            Vector3 size = new Vector3(limitX.y - limitX.x, ceilingY - seaLevelY, limitZ.y - limitZ.x);

            Gizmos.DrawWireCube(center, size);
        }
    }
}