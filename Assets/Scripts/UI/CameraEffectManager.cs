using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JetSimulation.Core;

namespace JetSimulation.UI
{
    public sealed class CameraEffectManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("화면 테두리가 붉어질 피격 이펙트 이미지")]
        [SerializeField] private Image vignetteImage;
        [Tooltip("게임 오버 시 서서히 어두워질 검은색 화면 이미지")]
        [SerializeField] private Image fadeImage;

        [Header("Settings")]
        [Tooltip("피격 효과가 깜빡이는 시간")]
        [SerializeField] private float vignetteDuration = 0.5f;
        [Tooltip("피격 시 최대 투명도 (0.0 ~ 1.0)")]
        [SerializeField] private float maxVignetteAlpha = 0.6f;
        [Tooltip("게임 오버 암전에 걸리는 시간")]
        [SerializeField] private float fadeDuration = 2f;

        private Coroutine vignetteCoroutine;

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                // PlayerHealth의 이벤트 구독
                playerHealth.OnTookDamage += ShowDamageEffect;
                playerHealth.OnPlayerDied += ShowGameOverFade;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnTookDamage -= ShowDamageEffect;
                playerHealth.OnPlayerDied -= ShowGameOverFade;
            }
        }

        // --- 피격 효과 (붉은 화면 깜빡임) ---
        private void ShowDamageEffect()
        {
            if (vignetteImage == null) return;

            if (vignetteCoroutine != null)
                StopCoroutine(vignetteCoroutine);

            vignetteCoroutine = StartCoroutine(VignetteRoutine());
        }

        private IEnumerator VignetteRoutine()
        {
            float elapsed = 0f;
            Color c = vignetteImage.color;
            float halfDuration = vignetteDuration / 2f;

            // 1. 붉어짐 (Fade In)
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0f, maxVignetteAlpha, elapsed / halfDuration);
                vignetteImage.color = c;
                yield return null;
            }

            // 2. 다시 투명해짐 (Fade Out)
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(maxVignetteAlpha, 0f, elapsed / halfDuration);
                vignetteImage.color = c;
                yield return null;
            }

            c.a = 0f;
            vignetteImage.color = c;
        }

        // --- 게임 오버 암전 (서서히 까매짐) ---
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

            // 암전이 끝나면 최종 결과창(UI)을 띄우는 로직 연동 가능
            // UIManager.Instance.ShowPanel(UIPanelType.GameOver);
        }
    }
}