using UnityEngine;
using System;
using JetSimulation.Core;

namespace JetSimulation.Environment
{
    public sealed class FlightBoundaryManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerHealth playerHealth;

        [Tooltip("방금 만든 시각화용 Sphere 오브젝트를 넣어주세요")]
        [SerializeField] private Transform boundaryVisualizer;

        [Header("Boundary Settings")]
        [SerializeField] private Vector3 centerPoint = Vector3.zero;
        [SerializeField] private float maxRadius = 2000f;
        [SerializeField] private float warningTime = 5f;

        private bool isOutOfBounds = false;
        private float timer = 0f;

        private Material visualizerMat;
        private Color normalColor = new Color(0f, 0.5f, 1f, 0.1f); // 평소: 반투명 파란색
        private Color warningColor = new Color(1f, 0f, 0f, 0.3f);  // 경고: 반투명 빨간색

        public event Action<float> OnWarningTimerUpdated;
        public event Action<bool> OnBoundaryStateChanged;

        private void Start()
        {
            // 시작할 때 구체의 크기를 maxRadius에 맞춰 자동으로 세팅 (반지름 * 2 = 지름)
            if (boundaryVisualizer != null)
            {
                boundaryVisualizer.localScale = Vector3.one * (maxRadius * 2f);
                boundaryVisualizer.position = centerPoint;

                // 머티리얼 색상 제어를 위해 캐싱
                Renderer renderer = boundaryVisualizer.GetComponent<Renderer>();
                if (renderer != null)
                {
                    visualizerMat = renderer.material;
                    visualizerMat.color = normalColor;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null || playerHealth == null) return;

            float distanceFromCenter = Vector3.Distance(centerPoint, playerTransform.position);

            if (distanceFromCenter > maxRadius)
            {
                if (!isOutOfBounds)
                {
                    isOutOfBounds = true;
                    timer = warningTime;
                    OnBoundaryStateChanged?.Invoke(true);

                    if (visualizerMat != null) visualizerMat.color = warningColor;
                }

                timer -= Time.deltaTime;
                OnWarningTimerUpdated?.Invoke(timer);

                if (timer <= 0f)
                {
                    playerHealth.TakeDamage(9999f, gameObject);
                    enabled = false;
                }
            }
            else
            {
                if (isOutOfBounds)
                {
                    isOutOfBounds = false;
                    OnBoundaryStateChanged?.Invoke(false);

                    if (visualizerMat != null) visualizerMat.color = normalColor;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(centerPoint, maxRadius);
        }
    }
}