using UnityEngine;

namespace JetSimulation.UI
{
    public sealed class CenterReferenceHUD : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Color hudColor = Color.green;
        [SerializeField, Min(1f)] private float lineThickness = 10f;
        [SerializeField, Min(4f)] private float wingLength = 34f;
        [SerializeField, Min(0f)] private float centerGap = 9f;
        [SerializeField, Min(4f)] private float verticalLength = 16f;

        private bool isBuilt;
        private bool rebuildRequested;

        private void Start()
        {
            Build();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                rebuildRequested = true;
        }

        private void LateUpdate()
        {
            if (!rebuildRequested)
                return;

            rebuildRequested = false;
            Rebuild();
        }

        public void ApplyDefaults(Color color)
        {
            hudColor = color;
            HudGraphicFactory.ApplyColorToGraphics(transform, hudColor);
            Build();
        }

        public void Rebuild()
        {
            ClearChildren();
            isBuilt = false;
            Build();
        }

        private void Build()
        {
            if (isBuilt)
                return;

            ClearChildren();
            HudGraphicFactory.CreateLine("LeftWing", transform, new Vector2(-(centerGap + wingLength * 0.5f), 0f), new Vector2(wingLength, lineThickness), hudColor);
            HudGraphicFactory.CreateLine("RightWing", transform, new Vector2(centerGap + wingLength * 0.5f, 0f), new Vector2(wingLength, lineThickness), hudColor);
            HudGraphicFactory.CreateLine("TopPost", transform, new Vector2(0f, centerGap + verticalLength * 0.5f), new Vector2(lineThickness, verticalLength), hudColor);
            HudGraphicFactory.CreateLine("CenterDot", transform, Vector2.zero, new Vector2(lineThickness + 2f, lineThickness + 2f), hudColor);
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
        }
    }
}
