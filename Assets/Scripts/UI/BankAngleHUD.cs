using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JetSimulation.UI
{
    public sealed class BankAngleHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform aircraftTransform;

        [Header("Visual")]
        [SerializeField] private Color hudColor = new Color(0.15f, 1f, 0.78f, 0.9f);
        [SerializeField, Min(40f)] private float radius = 150f;
        [SerializeField, Min(1f)] private float lineThickness = 5f;
        [SerializeField, Min(4f)] private float minorTickLength = 11f;
        [SerializeField, Min(4f)] private float majorTickLength = 18f;
        [SerializeField, Min(4f)] private float markerLength = 24f;
        [SerializeField] private float yOffset = 168f;
        [SerializeField] private float markerRotationSign = -1f;

        [Header("Simplified Bank Indicator")]
        [SerializeField, Range(15f, 90f)] private float maxDisplayedBankAngle = 60f;
        [SerializeField, Range(45f, 120f)] private float warningBankAngle = 60f;
        [SerializeField, Range(90f, 180f)] private float invertedBankAngle = 120f;
        [SerializeField] private Color warningColor = new Color(1f, 0.86f, 0.18f, 0.95f);
        [SerializeField] private Color invertedColor = new Color(1f, 0.24f, 0.08f, 0.98f);

        private RectTransform markerRoot;
        private Image markerImage;
        private TextMeshProUGUI invertedLabel;
        private bool isBuilt;

        private void Start()
        {
            Build();
        }

        private void LateUpdate()
        {
            if (markerRoot == null || aircraftTransform == null)
                return;

            float roll = FlightHudMath.GetRollDegrees(aircraftTransform);
            float displayedRoll = Mathf.Clamp(roll, -maxDisplayedBankAngle, maxDisplayedBankAngle);
            markerRoot.localEulerAngles = new Vector3(0f, 0f, displayedRoll * markerRotationSign);
            UpdateStateVisuals(roll);
        }

        public void ApplyDefaults(Transform aircraft, Color color)
        {
            if (aircraftTransform == null)
                aircraftTransform = aircraft;

            hudColor = color;
            HudGraphicFactory.ApplyColorToGraphics(transform, hudColor);
            Build();
        }

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            markerRoot = null;
            markerImage = null;
            invertedLabel = null;
            isBuilt = false;
        }

        private void Build()
        {
            if (isBuilt)
                return;

            ClearChildren();
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(0f, yOffset);

            for (int angle = -60; angle <= 60; angle += 15)
            {
                bool major = angle == 0 || Mathf.Abs(angle) == 30 || Mathf.Abs(angle) == 60;
                float tickLength = major ? majorTickLength : minorTickLength;
                Vector2 position = GetArcPosition(angle, radius);
                HudGraphicFactory.CreateLine($"BankTick_{angle}", transform, position, new Vector2(lineThickness, tickLength), hudColor, -angle);
            }

            markerRoot = HudGraphicFactory.CreateRoot("BankMarkerRoot", transform);
            Vector2 markerPosition = GetArcPosition(0f, radius - markerLength * 0.5f);
            markerImage = HudGraphicFactory.CreateLine("BankMarker", markerRoot, markerPosition, new Vector2(lineThickness + 2f, markerLength), hudColor);

            invertedLabel = HudGraphicFactory.CreateText("InvertedLabel", transform, "INV", new Vector2(0f, radius + 34f), 22f, invertedColor);
            invertedLabel.fontStyle = FontStyles.Bold;
            invertedLabel.gameObject.SetActive(false);

            isBuilt = true;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            markerImage = null;
            invertedLabel = null;
        }

        private void UpdateStateVisuals(float roll)
        {
            float absoluteRoll = Mathf.Abs(roll);
            bool isInverted = absoluteRoll >= invertedBankAngle;
            bool isWarning = absoluteRoll >= warningBankAngle;
            Color stateColor = isInverted ? invertedColor : isWarning ? warningColor : hudColor;

            if (markerImage != null)
                markerImage.color = stateColor;

            if (invertedLabel != null)
            {
                invertedLabel.gameObject.SetActive(isInverted);
                invertedLabel.color = stateColor;
            }
        }

        private static Vector2 GetArcPosition(float angle, float arcRadius)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians) * arcRadius, Mathf.Cos(radians) * arcRadius);
        }
    }
}
