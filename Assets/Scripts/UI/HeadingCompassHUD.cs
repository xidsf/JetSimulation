using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JetSimulation.UI
{
    public sealed class HeadingCompassHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform aircraftTransform;

        [Header("Visual")]
        [SerializeField] private Color hudColor = new Color(0.15f, 1f, 0.78f, 0.9f);
        [SerializeField, Min(80f)] private float visibleWidth = 640f;
        [SerializeField, Min(1f)] private float pixelsPerDegree = 5f;
        [SerializeField, Min(5f)] private float tickStepDegrees = 15f;
        [SerializeField, Min(30f)] private float displayRangeDegrees = 75f;
        [SerializeField, Min(1f)] private float lineThickness = 5f;
        [SerializeField, Min(4f)] private float minorTickLength = 13f;
        [SerializeField, Min(4f)] private float majorTickLength = 24f;
        [SerializeField, Min(8f)] private float labelFontSize = 22f;
        [SerializeField, Min(0f)] private float topOffset = 64f;

        private RectTransform tickRoot;
        private TextMeshProUGUI headingText;
        private readonly List<TickSlot> tickSlots = new List<TickSlot>();
        private bool isBuilt;

        private void Start()
        {
            Build();
        }

        private void LateUpdate()
        {
            if (aircraftTransform == null)
                return;

            BuildCompass(FlightHudMath.GetHeadingDegrees(aircraftTransform));
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

            tickRoot = null;
            headingText = null;
            tickSlots.Clear();
            isBuilt = false;
        }

        private void Build()
        {
            if (isBuilt)
                return;

            ClearChildren();
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null)
                ApplyTopAnchor(rectTransform);

            HudGraphicFactory.CreateLine("CompassCenterMarker", transform, new Vector2(0f, -10f), new Vector2(lineThickness + 1f, 42f), hudColor);
            HudGraphicFactory.CreateLine("CompassBaseline", transform, Vector2.zero, new Vector2(visibleWidth, lineThickness), hudColor);

            tickRoot = HudGraphicFactory.CreateRoot("CompassTickRoot", transform);
            headingText = HudGraphicFactory.CreateText("HeadingValue", transform, "000", new Vector2(0f, 34f), labelFontSize, hudColor);
            headingText.fontStyle = FontStyles.Bold;
            EnsureTickSlots();

            isBuilt = true;
        }

        private void BuildCompass(float heading)
        {
            EnsureTickSlots();

            float start = Mathf.Floor((heading - displayRangeDegrees) / tickStepDegrees) * tickStepDegrees;
            float end = heading + displayRangeDegrees;
            int slotIndex = 0;

            for (float mark = start; mark <= end; mark += tickStepDegrees)
            {
                if (slotIndex >= tickSlots.Count)
                    break;

                float normalizedMark = Mathf.Repeat(mark, 360f);
                float delta = Mathf.DeltaAngle(heading, normalizedMark);
                float x = delta * pixelsPerDegree;
                bool major = Mathf.Approximately(Mathf.Repeat(normalizedMark, 30f), 0f);
                float tickLength = major ? majorTickLength : minorTickLength;
                TickSlot slot = tickSlots[slotIndex++];

                slot.Root.gameObject.SetActive(true);
                slot.Root.anchoredPosition = new Vector2(x, 0f);
                slot.Tick.rectTransform.anchoredPosition = new Vector2(0f, -tickLength * 0.5f);
                slot.Tick.rectTransform.sizeDelta = new Vector2(lineThickness, tickLength);
                slot.Tick.color = hudColor;

                if (major)
                {
                    slot.Label.gameObject.SetActive(true);
                    slot.Label.text = GetHeadingLabel(normalizedMark);
                    slot.Label.fontSize = labelFontSize;
                    slot.Label.color = hudColor;
                }
                else
                {
                    slot.Label.gameObject.SetActive(false);
                }
            }

            for (int i = slotIndex; i < tickSlots.Count; i++)
                tickSlots[i].Root.gameObject.SetActive(false);

            headingText.text = Mathf.RoundToInt(heading).ToString("000");
        }

        private void EnsureTickSlots()
        {
            if (tickRoot == null)
                return;

            int neededCount = Mathf.CeilToInt(displayRangeDegrees * 2f / tickStepDegrees) + 3;
            while (tickSlots.Count < neededCount)
                tickSlots.Add(CreateTickSlot(tickSlots.Count));
        }

        private TickSlot CreateTickSlot(int index)
        {
            RectTransform slotRoot = HudGraphicFactory.CreateRoot($"HeadingSlot_{index}", tickRoot);
            Image tick = HudGraphicFactory.CreateLine("Tick", slotRoot, Vector2.zero, new Vector2(lineThickness, majorTickLength), hudColor);
            TextMeshProUGUI label = HudGraphicFactory.CreateText("Label", slotRoot, string.Empty, new Vector2(0f, -42f), labelFontSize, hudColor);
            label.rectTransform.sizeDelta = new Vector2(72f, 24f);
            label.gameObject.SetActive(false);

            return new TickSlot(slotRoot, tick, label);
        }

        private void ApplyTopAnchor(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -topOffset);
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

            tickSlots.Clear();
            tickRoot = null;
            headingText = null;
        }

        private static string GetHeadingLabel(float heading)
        {
            int rounded = Mathf.RoundToInt(Mathf.Repeat(heading, 360f));
            switch (rounded)
            {
                case 0:
                case 360:
                    return "N";
                case 90:
                    return "E";
                case 180:
                    return "S";
                case 270:
                    return "W";
                default:
                    return rounded.ToString("000");
            }
        }

        private sealed class TickSlot
        {
            public TickSlot(RectTransform root, Image tick, TextMeshProUGUI label)
            {
                Root = root;
                Tick = tick;
                Label = label;
            }

            public RectTransform Root { get; }
            public Image Tick { get; }
            public TextMeshProUGUI Label { get; }
        }
    }
}
