using UnityEngine;
using System;
using JetSimulation.Core;
using JetSimulation.UI;

namespace JetSimulation.Environment
{
    public sealed class FlightBoundaryManager : MonoBehaviour
    {
        [Header("Boundary Settings")]
        [Tooltip("작전 구역의 최대 반경")]
        [SerializeField] private float maxRadius = 2000f;
        [Tooltip("이탈 허용 시간 (초)")]
        [SerializeField] private float warningTimeLimit = 5f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform boundaryVisualizer;

        // 이벤트 정의
        public event Action<bool> OnBoundaryStateChanged;
        //  허공 텍스트에 남은 시간을 알려주는 이벤트
        public event Action<float> OnWarningTimerUpdated;

        private bool isOutOfBounds = false;
        private float currentWarningTime = 0f;

        private void Start()
        {
            // 게임 시작 시 인스펙터에 설정된 maxRadius 값으로 시각화 오브젝트 크기 초기화
            SetBoundaryRadius(maxRadius);
        }
        private void Update()
        {
            if (playerTransform == null || playerHealth == null || playerHealth.IsDead) return;

            float distance = Vector3.Distance(Vector3.zero, playerTransform.position);
            bool currentlyOutOfBounds = distance > maxRadius;

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

                // 매 프레임 남은 시간을 UI 텍스트로 전달
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
                boundaryVisualizer.localScale = Vector3.one * (maxRadius * 2f);
            }
        }
    }
}