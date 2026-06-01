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

        [Tooltip("카메라 자식으로 넣은 3D 구체(FadeSphere)를 넣어주세요")]
        [SerializeField] private Renderer fadeRenderer;

        [Tooltip("배경과 함께 서서히 나타날 텍스트 (작전 실패)")]
        [SerializeField] private TextMeshProUGUI gameOverText;

        [Tooltip("완전히 암전된 후 나타날 버튼들을 넣어주세요")]
        [SerializeField] private GameObject[] gameOverButtons;

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

        private IEnumerator WarningPulseRoutine()
        {
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

        // --- 3. 3D 구체를 활용한 암전 및 UIManager 연동 ---
        private void ShowGameOverFade()
        {
            // 1. 암전용 3D 구체 활성화
            if (fadeRenderer != null)
            {
                fadeRenderer.gameObject.SetActive(true);
            }

            if (UIManager.Instance != null)
            {
                // EnablePanelOverlay 대신 ShowPanel을 사용하면 HUD가 자동으로 꺼집니다!
                UIManager.Instance.ShowPanel(UIPanelType.GameOver);
            }

            // 3. 서서히 암전되면서 텍스트가 나타나는 코루틴 실행
            StartCoroutine(FadeOutAndShowUIRoutine());
        }

        private IEnumerator FadeOutAndShowUIRoutine()
        {
            float elapsed = 0f;

            // 검은 구체의 알파값을 0으로 세팅
            Color bgColor = Color.black;
            if (fadeRenderer != null)
            {
                bgColor = fadeRenderer.material.color;
                bgColor.a = 0f;
                fadeRenderer.material.color = bgColor;
            }

            // 텍스트의 알파값을 0으로 세팅
            Color textColor = Color.white;
            if (gameOverText != null)
            {
                textColor = gameOverText.color;
                textColor.a = 0f;
                gameOverText.color = textColor;
                gameOverText.gameObject.SetActive(true);
            }

            // 페이드 도중에는 버튼이 보이면 안 되므로 강제로 숨김
            foreach (var btn in gameOverButtons)
            {
                if (btn != null) btn.SetActive(false);
            }

            // 2초 동안 서서히 암전 & 텍스트 페이드 인
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);

                if (fadeRenderer != null)
                {
                    bgColor.a = alpha;
                    fadeRenderer.material.color = bgColor;
                }

                if (gameOverText != null)
                {
                    textColor.a = alpha;
                    gameOverText.color = textColor;
                }

                yield return null;
            }

            // 완전히 까매진 후 버튼 활성화
            foreach (var btn in gameOverButtons)
            {
                if (btn != null) btn.SetActive(true);
            }
        }
    }
}