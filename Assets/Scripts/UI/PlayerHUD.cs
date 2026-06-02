using UnityEngine;
using UnityEngine.UI; // Slider를 사용하기 위해 필요
using JetSimulation.Core;

namespace JetSimulation.UI
{
    public sealed class PlayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;

        [Tooltip("유니티 기본 Slider UI")]
        [SerializeField] private Slider hpSlider;

        [Header("Settings")]
        [SerializeField] private float fillSpeed = 5f;

        private float targetSliderValue = 1f;

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += UpdateHealthBar;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= UpdateHealthBar;
            }
        }

        private void UpdateHealthBar(float healthRatio)
        {
            // Slider의 value는 기본적으로 0 ~ 1 사이를 사용하도록 설정해야 합니다.
            targetSliderValue = healthRatio;
        }

        private void Update()
        {
            if (hpSlider != null)
            {
                // Slider의 value를 부드럽게 Lerp 처리
                hpSlider.value = Mathf.Lerp(hpSlider.value, targetSliderValue, Time.deltaTime * fillSpeed);
            }
        }
    }
}