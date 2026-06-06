using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JetSimulation.UI
{
    public sealed class AttitudePitchLadderHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform aircraftTransform;

        [Header("Visual")]
        [SerializeField] private Color hudColor = new Color(0.15f, 1f, 0.78f, 0.9f);
        [SerializeField, Min(2f)] private float lineThickness = 5f;
        [SerializeField, Min(40f)] private float horizonLineLength = 360f;
        [SerializeField, Min(20f)] private float ladderLineLength = 150f;
        [SerializeField, Min(1f)] private float pixelsPerDegree = 7f;
        [SerializeField, Min(8f)] private float labelFontSize = 24f;
        [SerializeField] private float rollRotationSign = -1f;
        [SerializeField] private float pitchOffsetSign = -1f;

        [Header("Simplified Pitch Indicator")]
        [SerializeField, Range(30f, 89f)] private float maxDisplayedPitchAngle = 45f;
        [SerializeField, Range(45f, 89f)] private float loopPitchAngle = 75f;
        [SerializeField] private Color loopColor = new Color(1f, 0.86f, 0.18f, 0.95f);

        private readonly int[] pitchMarks = { -30, -15, 0, 15, 30 };
        private RectTransform movingRoot;
        private TextMeshProUGUI loopLabel;
        private Graphic[] ladderGraphics;
        private bool isBuilt;

        private void Start()
        {
            Build();
        }

        private void LateUpdate()
        {
            if (movingRoot == null || aircraftTransform == null)
                return;

            float pitch = FlightHudMath.GetPitchDegrees(aircraftTransform);
            float roll = FlightHudMath.GetRollDegrees(aircraftTransform);
            float displayedPitch = Mathf.Clamp(pitch, -maxDisplayedPitchAngle, maxDisplayedPitchAngle);

            movingRoot.anchoredPosition = new Vector2(0f, displayedPitch * pixelsPerDegree * pitchOffsetSign);
            movingRoot.localEulerAngles = new Vector3(0f, 0f, roll * rollRotationSign);
            UpdateLoopVisuals(pitch);
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

            movingRoot = null;
            loopLabel = null;
            ladderGraphics = null;
            isBuilt = false;
        }

        private void Build()
        {
            if (isBuilt)
                return;

            ClearChildren();
            movingRoot = HudGraphicFactory.CreateRoot("PitchLadderMovingRoot", transform);

            for (int i = 0; i < pitchMarks.Length; i++)
            {
                int mark = pitchMarks[i];
                float y = mark * pixelsPerDegree;
                float length = mark == 0 ? horizonLineLength : ladderLineLength;
                string lineName = mark == 0 ? "HorizonLine" : $"PitchLine_{mark}";

                HudGraphicFactory.CreateLine(lineName, movingRoot, new Vector2(0f, y), new Vector2(length, lineThickness), hudColor);

                if (mark != 0)
                    CreatePitchLabel(mark, y);
                else
                    HudGraphicFactory.CreateText("PitchLabel_0", movingRoot, "0", new Vector2(length * 0.5f + 24f, y), labelFontSize, hudColor);
            }

            loopLabel = HudGraphicFactory.CreateText("LoopLabel", transform, "LOOP", new Vector2(0f, maxDisplayedPitchAngle * pixelsPerDegree + 46f), labelFontSize, loopColor);
            loopLabel.fontStyle = FontStyles.Bold;
            loopLabel.gameObject.SetActive(false);
            ladderGraphics = movingRoot.GetComponentsInChildren<Graphic>(true);

            isBuilt = true;
        }

        private void CreatePitchLabel(int mark, float y)
        {
            string text = mark > 0 ? $"+{mark}" : mark.ToString();
            float labelX = ladderLineLength * 0.5f + 28f;
            TextMeshProUGUI rightLabel = HudGraphicFactory.CreateText($"PitchLabelRight_{mark}", movingRoot, text, new Vector2(labelX, y), labelFontSize, hudColor);
            rightLabel.alignment = TextAlignmentOptions.Left;

            TextMeshProUGUI leftLabel = HudGraphicFactory.CreateText($"PitchLabelLeft_{mark}", movingRoot, text, new Vector2(-labelX, y), labelFontSize, hudColor);
            leftLabel.alignment = TextAlignmentOptions.Right;
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

            loopLabel = null;
            ladderGraphics = null;
        }

        private void UpdateLoopVisuals(float pitch)
        {
            bool isLoopPitch = Mathf.Abs(pitch) >= loopPitchAngle;
            Color stateColor = isLoopPitch ? loopColor : hudColor;

            if (ladderGraphics != null)
            {
                for (int i = 0; i < ladderGraphics.Length; i++)
                    ladderGraphics[i].color = stateColor;
            }

            if (loopLabel != null)
            {
                loopLabel.gameObject.SetActive(isLoopPitch);
                loopLabel.color = stateColor;
            }
        }
    }
}
