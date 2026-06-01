using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JetSimulation.Core;
using JetSimulation.Environment; // 추가: 맵 이탈 매니저 참조용

namespace JetSimulation.UI
{
    public sealed class CameraEffectManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("방금 만든 맵 이탈 감지 매니저를 연결해주세요")]
        [SerializeField] private FlightBoundaryManager boundaryManager;

        [SerializeField] private Image vignetteImage;
        [SerializeField] private Image fadeImage;

        [Header("Damage Settings (피격)")]
        [SerializeField] private float damageVignetteDuration = 0.5f;
        [SerializeField] private float maxDamageAlpha = 0.8f; // 피격 시 강한 붉은색

        [Header("Warning Settings (맵 이탈 경고)")]
        [SerializeField] private float warningPulseSpeed = 3f; // 깜빡이는 속도
        [SerializeField] private float maxWarningAlpha = 0.4f; // 경고 시 은은한 붉은색

        [Header("Game Over Settings")]
        [SerializeField] private float fadeDuration = 2f;

        private Coroutine effectCoroutine;
        private bool isOutOfBounds = false; // 현재 맵 밖에 있는지 여부

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnTookDamage += ShowDamageEffect;
                playerHealth.OnPlayerDied += ShowGameOverFade;
            }
            if (boundaryManager != null)
            {
                // 맵 이탈 신호 구독
                boundaryManager.OnBoundaryStateChanged += HandleBoundaryWarning;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnTookDamage -= ShowDamageEffect;
                playerHealth.OnPlayerDied -= ShowGameOverFade;
            }
            if (boundaryManager != null)
            {
                boundaryManager.OnBoundaryStateChanged -= HandleBoundaryWarning;
            }
        }

        // --- 1. 맵 이탈 경고 (심장 박동 펄스) ---
        private void HandleBoundaryWarning(bool isWarning)
        {
            isOutOfBounds = isWarning;

            if (isWarning)
            {
                if (effectCoroutine != null) StopCoroutine(effectCoroutine);
                effectCoroutine = StartCoroutine(WarningPulseRoutine());
            }
            else
            {
                // 안전 구역으로 돌아오면 코루틴을 끄고 투명하게 복구
                if (effectCoroutine != null) StopCoroutine(effectCoroutine);
                ResetVignette();
            }
        }

        private IEnumerator WarningPulseRoutine()
        {
            Color c = vignetteImage.color;
            while (isOutOfBounds)
            {
                // PingPong을 사용해 0.1 ~ maxWarningAlpha 사이를 부드럽게 오르락내리락
                c.a = Mathf.Lerp(0.05f, maxWarningAlpha, Mathf.PingPong(Time.time * warningPulseSpeed, 1f));
                vignetteImage.color = c;
                yield return null; // 매 프레임 실행
            }
        }

        // --- 2. 피격 효과 (우선순위 높음) ---
        private void ShowDamageEffect()
        {
            if (vignetteImage == null) return;

            // 경고 펄스 중이더라도 피격 효과가 덮어씌워지도록 기존 코루틴 중지
            if (effectCoroutine != null) StopCoroutine(effectCoroutine);
            effectCoroutine = StartCoroutine(DamageVignetteRoutine());
        }

        private IEnumerator DamageVignetteRoutine()
        {
            float elapsed = 0f;
            Color c = vignetteImage.color;
            float halfDuration = damageVignetteDuration / 2f;

            // 빠르게 번쩍! (Fade In)
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0f, maxDamageAlpha, elapsed / halfDuration);
                vignetteImage.color = c;
                yield return null;
            }

            // 다시 투명해짐 (Fade Out)
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(maxDamageAlpha, 0f, elapsed / halfDuration);
                vignetteImage.color = c;
                yield return null;
            }

            ResetVignette();

            // 피격 효과가 끝났는데 여전히 맵 밖이라면, 다시 경고 펄스 시작!
            if (isOutOfBounds)
            {
                effectCoroutine = StartCoroutine(WarningPulseRoutine());
            }
        }

        private void ResetVignette()
        {
            if (vignetteImage != null)
            {
                Color c = vignetteImage.color;
                c.a = 0f;
                vignetteImage.color = c;
            }
        }

        // --- 3. 게임 오버 암전 ---
        private void ShowGameOverFade()
        {
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(true);
                StartCoroutine(FadeOutRoutine());
            }
        }

        private IEnumerator FadeOutRoutine()
        {
            float elapsed = 0f;
            Color c = fadeImage.color;
            c.a = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                fadeImage.color = c;
                yield return null;
            }
        }
    }
}