using UnityEngine;
using UnityEngine.UI;

namespace JetSimulation.Combat
{
    public sealed class MissileLockOnReticleUI : MonoBehaviour
    {
        [SerializeField] Color flashColorA = new Color(1f, 0.1f, 0.05f, 1f);
        [SerializeField] Color flashColorB = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] Color releaseColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] float lockingFlashSpeed = 4f;
        [SerializeField] float lockedFlashSpeed = 10f;
        [SerializeField] float lineThickness = 4f;
        [SerializeField] float startSize = 80f;
        [SerializeField] float maxSize = 260f;
        [SerializeField] float fallbackSize = 80f;

        Canvas canvas;
        RectTransform root;
        Image top;
        Image bottom;
        Image left;
        Image right;
        Sprite lineSprite;
        float releaseStartSize;

        public void SetInitialSize(float size)
        {
            startSize = Mathf.Max(1f, size);
        }

        public void Show(Transform target, float progress, bool locked, Camera targetCamera)
        {
            if (target == null || targetCamera == null)
            {
                Hide();
                return;
            }

            EnsureUi(targetCamera);

            if (!TryGetTargetScreenRect(target, targetCamera, out var targetRect))
            {
                Hide();
                return;
            }

            var targetSize = Mathf.Min(Mathf.Max(targetRect.width, targetRect.height), maxSize);
            if (targetSize <= 0f)
                targetSize = fallbackSize;

            var acquisitionSize = Mathf.Max(startSize, targetSize);
            var size = Mathf.Lerp(acquisitionSize, targetSize, Mathf.Clamp01(progress));
            var center = targetRect.center;
            root.gameObject.SetActive(true);
            root.anchoredPosition = new Vector2(center.x - Screen.width * 0.5f, center.y - Screen.height * 0.5f);
            root.sizeDelta = new Vector2(size, size);

            ApplyLineLayout(size);
            ApplyColor(GetFlashColor(locked));
        }

        public void Hide()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        public bool BeginRelease()
        {
            if (root == null || !root.gameObject.activeSelf)
                return false;

            releaseStartSize = Mathf.Max(root.sizeDelta.x, root.sizeDelta.y);
            ApplyColor(releaseColor);
            return true;
        }

        public void ShowRelease(float progress)
        {
            if (root == null || !root.gameObject.activeSelf)
                return;

            var size = Mathf.Lerp(releaseStartSize, startSize, Mathf.Clamp01(progress));
            root.sizeDelta = new Vector2(size, size);
            ApplyLineLayout(size);
            ApplyColor(releaseColor);
        }

        void EnsureUi(Camera targetCamera)
        {
            if (root != null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                    canvas.worldCamera = targetCamera;

                return;
            }

            lineSprite = CreateLineSprite();

            var canvasObject = new GameObject("Missile Lock-On Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var rootObject = new GameObject("Lock-On Reticle", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            top = CreateLine("Top", root);
            bottom = CreateLine("Bottom", root);
            left = CreateLine("Left", root);
            right = CreateLine("Right", root);
            Hide();
        }

        Image CreateLine(string lineName, Transform parent)
        {
            var lineObject = new GameObject(lineName, typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(parent, false);
            var image = lineObject.GetComponent<Image>();
            image.sprite = lineSprite;
            image.raycastTarget = false;
            return image;
        }

        void ApplyLineLayout(float size)
        {
            SetHorizontal(top.rectTransform, size, size * 0.5f - lineThickness * 0.5f);
            SetHorizontal(bottom.rectTransform, size, -size * 0.5f + lineThickness * 0.5f);
            SetVertical(left.rectTransform, size, -size * 0.5f + lineThickness * 0.5f);
            SetVertical(right.rectTransform, size, size * 0.5f - lineThickness * 0.5f);
        }

        void SetHorizontal(RectTransform rectTransform, float width, float y)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, y);
            rectTransform.sizeDelta = new Vector2(width, lineThickness);
        }

        void SetVertical(RectTransform rectTransform, float height, float x)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(x, 0f);
            rectTransform.sizeDelta = new Vector2(lineThickness, height);
        }

        void ApplyColor(Color color)
        {
            top.color = color;
            bottom.color = color;
            left.color = color;
            right.color = color;
        }

        Color GetFlashColor(bool locked)
        {
            var speed = locked ? lockedFlashSpeed : lockingFlashSpeed;
            var pulse = Mathf.PingPong(Time.unscaledTime * speed, 1f);
            return Color.Lerp(flashColorA, flashColorB, pulse);
        }

        bool TryGetTargetScreenRect(Transform target, Camera targetCamera, out Rect rect)
        {
            var bounds = GetTargetBounds(target);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var hasVisiblePoint = false;

            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            for (var z = 0; z <= 1; z++)
            {
                var worldPoint = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);

                var screenPoint = targetCamera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z <= 0f)
                    continue;

                hasVisiblePoint = true;
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            if (!hasVisiblePoint)
            {
                rect = default;
                return false;
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        Bounds GetTargetBounds(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                return bounds;
            }

            var colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                var bounds = colliders[0].bounds;
                for (var i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);

                return bounds;
            }

            return new Bounds(target.position, Vector3.one);
        }

        static Sprite CreateLineSprite()
        {
            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
