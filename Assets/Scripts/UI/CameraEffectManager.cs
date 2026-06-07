using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using JetSimulation.Core;
using JetSimulation.Environment;

namespace JetSimulation.UI
{
    public sealed class CameraEffectManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private FlightBoundaryManager boundaryManager;
        [SerializeField] private Image vignetteImage;

        [Header("Damage Settings")]
        [SerializeField] private float damageVignetteDuration = 0.5f;
        [SerializeField] private float maxDamageAlpha = 0.8f;

        [Header("Warning Settings")]
        [SerializeField] private float warningPulseSpeed = 3f;
        [SerializeField] private float maxWarningAlpha = 0.4f;

        [Header("Game Over Settings")]
        [SerializeField] private float fadeDuration = 2f;
        [SerializeField] private Renderer fadeRenderer;
        [SerializeField] private TextMeshProUGUI gameOverText;
        [SerializeField] private GameObject[] gameOverButtons;
        [SerializeField] private GameObject finalScoreObject;

        private Coroutine effectCoroutine;
        private bool isOutOfBounds = false;

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnTookDamage += ShowDamageEffect;
                playerHealth.OnPlayerDied += ShowGameOverFade;
            }
            if (boundaryManager != null)
            {
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
                if (effectCoroutine != null) StopCoroutine(effectCoroutine);
                ResetVignette();
            }
        }

        // [복구 완료] 작전 구역 이탈 시 붉은 화면 깜빡임 연출
        private IEnumerator WarningPulseRoutine()
        {
            if (vignetteImage == null) yield break;

            Color c = vignetteImage.color;
            while (isOutOfBounds)
            {
                c.a = Mathf.Lerp(0.05f, maxWarningAlpha, Mathf.PingPong(Time.time * warningPulseSpeed, 1f));
                vignetteImage.color = c;
                yield return null;
            }
        }

        private void ShowDamageEffect()
        {
            // 핵심 추가: 이미 사망했다면 데미지 깜빡임 이펙트를 무시하여 암전 연출에 방해되지 않도록 함
            if (playerHealth != null && playerHealth.IsDead) return;

            if (vignetteImage == null) return;
            if (effectCoroutine != null) StopCoroutine(effectCoroutine);
            effectCoroutine = StartCoroutine(DamageVignetteRoutine());
        }

        private IEnumerator DamageVignetteRoutine()
        {
            float elapsed = 0f;
            Color c = vignetteImage.color;
            float halfDuration = damageVignetteDuration / 2f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0f, maxDamageAlpha, elapsed / halfDuration);
                vignetteImage.color = c;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(maxDamageAlpha, 0f, elapsed / halfDuration);
                vignetteImage.color = c;
                yield return null;
            }

            ResetVignette();

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

        private void ShowGameOverFade()
        {
            if (fadeRenderer != null)
            {
                fadeRenderer.gameObject.SetActive(true);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanelType.GameOver);
            }

            StartCoroutine(FadeOutAndShowUIRoutine());
        }

        private IEnumerator FadeOutAndShowUIRoutine()
        {
            float elapsed = 0f;

            Color bgColor = Color.black;
            if (fadeRenderer != null)
            {
                // URP 쉐이더 호환성을 위해 안전하게 _BaseColor에 접근
                if (fadeRenderer.material.HasProperty("_BaseColor"))
                {
                    bgColor = fadeRenderer.material.GetColor("_BaseColor");
                }
                else
                {
                    bgColor = fadeRenderer.material.color;
                }

                bgColor.a = 0f;

                if (fadeRenderer.material.HasProperty("_BaseColor"))
                    fadeRenderer.material.SetColor("_BaseColor", bgColor);
                else
                    fadeRenderer.material.color = bgColor;
            }

            Color textColor = Color.white;
            if (gameOverText != null)
            {
                textColor = gameOverText.color;
                textColor.a = 0f;
                gameOverText.color = textColor;
                gameOverText.gameObject.SetActive(true);
            }

            // 페이드 도중에는 버튼과 점수를 강제로 숨김
            foreach (var btn in gameOverButtons)
            {
                if (btn != null) btn.SetActive(false);
            }
            if (finalScoreObject != null) finalScoreObject.SetActive(false);

            // 2초 동안 서서히 암전 & 텍스트 페이드 인
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);

                if (fadeRenderer != null)
                {
                    bgColor.a = alpha;
                    if (fadeRenderer.material.HasProperty("_BaseColor"))
                        fadeRenderer.material.SetColor("_BaseColor", bgColor);
                    else
                        fadeRenderer.material.color = bgColor;
                }

                if (gameOverText != null)
                {
                    textColor.a = alpha;
                    gameOverText.color = textColor;
                }

                yield return null;
            }

            // 완전히 까매진 후 버튼과 점수 활성화
            foreach (var btn in gameOverButtons)
            {
                if (btn != null) btn.SetActive(true);
            }
            if (finalScoreObject != null) finalScoreObject.SetActive(true);
        }
    }
}