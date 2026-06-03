using JetSimulation.EnemySystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace JetSimulation.Combat
{
    public sealed class PlayerMachineGunController : MonoBehaviour
    {
        enum AimInputMode
        {
            MoveAction,
            ControllerTilt
        }

        enum TiltAxis
        {
            X,
            Y,
            Z
        }

        [Header("References")]
        [SerializeField] MissileAttackController missileAttackController;
        [FormerlySerializedAs("weaponAimCamera")]
        [SerializeField, InspectorName("Targeting Camera")] Camera targetingViewCamera;
        [FormerlySerializedAs("targetingCamera")]
        [SerializeField, InspectorName("Player View Camera")] Camera playerViewCamera;
        [SerializeField] Transform muzzlePoint;
        [SerializeField, InspectorName("Hitscan Smoke Effect")] ParticleSystem muzzleGasEffect;
        [SerializeField, InspectorName("Hitscan Smoke Prefab")] GameObject muzzleGasPrefab;
        [SerializeField] MachineGunCrosshairUI crosshairUI;

        [Header("Input")]
        [SerializeField] AimInputMode aimInputMode = AimInputMode.MoveAction;
        [SerializeField] InputActionReference aimMoveAction;
        [SerializeField] InputActionReference aimRotationAction;
        [SerializeField] InputActionReference fireAction;
        [SerializeField, Range(0.01f, 1f)] float fireThreshold = 0.5f;

        [Header("Move Action Aim")]
        [SerializeField, Min(0.01f)] float moveAimSpeed = 0.65f;

        [Header("Tilt Aim")]
        [SerializeField, Range(1f, 90f)] float maxTiltAngle = 35f;
        [SerializeField, Range(0f, 30f)] float tiltDeadAngle = 5f;
        [SerializeField] TiltAxis tiltHorizontalAxis = TiltAxis.Y;
        [SerializeField] TiltAxis tiltVerticalAxis = TiltAxis.X;
        [SerializeField] bool invertTiltHorizontal;
        [SerializeField] bool invertTiltVertical = true;
        [SerializeField, InspectorName("Tilt Aim Follow Speed"), Min(0.01f)] float tiltAimSpeed = 12f;
        [SerializeField, Min(0.1f)] float tiltResponseExponent = 1.35f;
        [SerializeField] bool autoCalibrateTiltNeutral = true;

        [Header("Crosshair")]
        [SerializeField] Vector2 initialViewportPosition = new Vector2(0.5f, 0.5f);
        [SerializeField, Range(0f, 0.45f)] float viewportMargin = 0.03f;
        [SerializeField, Min(0.01f)] float crosshairPlaneDistance = 120f;

        [Header("Hitscan")]
        [SerializeField] bool machineGunEnabled = true;
        [SerializeField, Min(0.01f)] float fireInterval = 0.08f;
        [SerializeField, Min(0f)] float damage = 4f;
        [SerializeField, Min(0.1f)] float maxDistance = 250f;
        [SerializeField] LayerMask hitLayers = ~0;
        [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Audio")]
        [SerializeField] AudioClip fireAudioClip;
        [SerializeField] AudioSource fireAudioSource;
        [SerializeField, Range(0f, 1f)] float fireAudioVolume = 0.8f;
        [SerializeField, Range(0.1f, 3f)] float fireAudioPitch = 1f;
        [SerializeField, Range(0f, 0.25f)] float fireAudioPitchJitter = 0.03f;

        [Header("Hitscan Smoke Visual")]
        [SerializeField] bool showHitscanSmoke = true;
        [SerializeField, Min(0.001f)] float smokeVisualScale = 0.01f;
        [SerializeField, Min(0.05f)] float smokeLifeTime = 0.45f;
        [SerializeField, InspectorName("Smoke Min Puff Count"), Range(1, 24)] int smokePuffCount = 7;
        [SerializeField, Min(0.001f)] float smokePuffSpacing = 0.06f;
        [SerializeField, Range(1, 16384)] int smokeMaxPuffCount = 2048;
        [SerializeField, Range(1, 8)] int smokeParticlesPerPuff = 1;
        [SerializeField, Min(0f)] float smokeStartOffset = 0.35f;
        [SerializeField, Min(0.1f)] float smokeMaxVisualDistance = 80f;
        [SerializeField, Min(0f)] float smokeSpreadRadius = 0.025f;
        [SerializeField] bool useScriptedSmokeVelocity;
        [SerializeField, Min(0f)] float smokeForwardDrift;
        [SerializeField, Min(0f)] float smokeTurbulence;
        [SerializeField, Min(0.001f)] float smokeStartSize = 4f;
        [SerializeField] Color smokeColor = new Color(0.75f, 0.88f, 1f, 0.5f);

        [Header("Debug")]
        [SerializeField] bool showDebugRay;
        [SerializeField] Color debugRayColor = Color.yellow;

        Vector2 crosshairViewportPosition;
        Quaternion leftControllerNeutralRotation;
        bool hasLeftControllerNeutral;
        XRController cachedLeftController;
        float nextFireTime;
        ParticleSystem hitscanSmokeEffectInstance;
        GameObject hitscanSmokeObjectInstance;
        bool ownsHitscanSmokeObjectInstance;
        bool hitscanSmokeConfigured;

        public Vector2 CrosshairViewportPosition => crosshairViewportPosition;

        void Awake()
        {
            ResolveReferences();
            crosshairViewportPosition = ClampViewport(initialViewportPosition);

            if (crosshairUI == null)
                crosshairUI = GetComponent<MachineGunCrosshairUI>();

            if (crosshairUI == null)
                crosshairUI = gameObject.AddComponent<MachineGunCrosshairUI>();
        }

        void OnEnable()
        {
            aimMoveAction?.action?.Enable();
            aimRotationAction?.action?.Enable();
            fireAction?.action?.Enable();
        }

        void OnDisable()
        {
            aimMoveAction?.action?.Disable();
            aimRotationAction?.action?.Disable();
            fireAction?.action?.Disable();

            if (crosshairUI != null)
                crosshairUI.Hide();

            if (hitscanSmokeEffectInstance != null)
                hitscanSmokeEffectInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDestroy()
        {
            if (ownsHitscanSmokeObjectInstance && hitscanSmokeObjectInstance != null)
                Destroy(hitscanSmokeObjectInstance);
        }

        void Update()
        {
            ResolveReferences();

            if (!machineGunEnabled || targetingViewCamera == null)
            {
                if (crosshairUI != null)
                    crosshairUI.Hide();
                return;
            }

            UpdateCrosshairPosition();
            UpdateCrosshairUI();

            if (IsFirePressed() && Time.time >= nextFireTime)
            {
                FireHitscan();
                nextFireTime = Time.time + fireInterval;
            }
        }

        void ResolveReferences()
        {
            if (missileAttackController == null)
                missileAttackController = GetComponent<MissileAttackController>();

            if (targetingViewCamera == null && missileAttackController != null)
                targetingViewCamera = missileAttackController.TargetingCamera;

            if (playerViewCamera == null && missileAttackController != null)
                playerViewCamera = missileAttackController.PlayerViewCamera;

            if (playerViewCamera == null)
                playerViewCamera = Camera.main;

            if (muzzlePoint == null)
                muzzlePoint = transform;
        }

        void UpdateCrosshairPosition()
        {
            if (aimInputMode == AimInputMode.ControllerTilt)
            {
                UpdateCrosshairFromTilt();
                return;
            }

            var aimInput = ReadMoveAimInput();
            var speed = moveAimSpeed;
            crosshairViewportPosition += aimInput * speed * Time.deltaTime;
            crosshairViewportPosition = ClampViewport(crosshairViewportPosition);
        }

        void UpdateCrosshairFromTilt()
        {
            var tiltInput = ReadTiltAimInput();
            var targetViewportPosition = TiltInputToViewportPosition(tiltInput);
            var followFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, tiltAimSpeed) * Time.deltaTime);
            crosshairViewportPosition = ClampViewport(Vector2.Lerp(crosshairViewportPosition, targetViewportPosition, followFactor));
        }

        Vector2 ReadMoveAimInput()
        {
            var action = aimMoveAction != null ? aimMoveAction.action : null;
            if (action != null)
                return Vector2.ClampMagnitude(action.ReadValue<Vector2>(), 1f);

            var leftController = GetLeftController();
            if (leftController != null)
            {
                var stick = leftController.TryGetChildControl<Vector2Control>("primary2DAxis");
                if (stick != null)
                    return Vector2.ClampMagnitude(stick.ReadValue(), 1f);
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            var input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;

            return Vector2.ClampMagnitude(input, 1f);
        }

        Vector2 ReadTiltAimInput()
        {
            if (!TryGetLeftControllerRotation(out var rotation))
                return Vector2.zero;

            if (!hasLeftControllerNeutral && autoCalibrateTiltNeutral)
            {
                leftControllerNeutralRotation = rotation;
                hasLeftControllerNeutral = true;
            }

            var neutral = hasLeftControllerNeutral ? leftControllerNeutralRotation.eulerAngles : Vector3.zero;
            var delta = GetEulerDelta(neutral, rotation.eulerAngles);
            var horizontal = RemapTiltAxis(delta, tiltHorizontalAxis, invertTiltHorizontal);
            var vertical = RemapTiltAxis(delta, tiltVerticalAxis, invertTiltVertical);
            return ApplyTiltResponse(Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f));
        }

        Vector2 TiltInputToViewportPosition(Vector2 tiltInput)
        {
            var margin = Mathf.Clamp(viewportMargin, 0f, 0.45f);
            return new Vector2(
                Mathf.Lerp(margin, 1f - margin, Mathf.InverseLerp(-1f, 1f, tiltInput.x)),
                Mathf.Lerp(margin, 1f - margin, Mathf.InverseLerp(-1f, 1f, tiltInput.y)));
        }

        Vector2 ApplyTiltResponse(Vector2 input)
        {
            return new Vector2(
                ApplyTiltResponse(input.x),
                ApplyTiltResponse(input.y));
        }

        float ApplyTiltResponse(float value)
        {
            var exponent = Mathf.Max(0.1f, tiltResponseExponent);
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
        }

        bool TryGetLeftControllerRotation(out Quaternion rotation)
        {
            var action = aimRotationAction != null ? aimRotationAction.action : null;
            if (action != null)
            {
                rotation = action.ReadValue<Quaternion>();
                return IsValidRotation(rotation);
            }

            var leftController = GetLeftController();
            if (leftController != null && leftController.deviceRotation != null)
            {
                rotation = leftController.deviceRotation.ReadValue();
                return IsValidRotation(rotation);
            }

            rotation = Quaternion.identity;
            return false;
        }

        float RemapTiltAxis(Vector3 delta, TiltAxis axis, bool invert)
        {
            var angle = axis switch
            {
                TiltAxis.X => delta.x,
                TiltAxis.Y => delta.y,
                TiltAxis.Z => delta.z,
                _ => 0f
            };

            var sign = invert ? -1f : 1f;
            var maxAngle = Mathf.Max(1f, maxTiltAngle);
            var deadAngle = Mathf.Clamp(tiltDeadAngle, 0f, maxAngle - 0.01f);
            var magnitude = Mathf.Abs(angle);
            if (magnitude <= deadAngle)
                return 0f;

            var normalized = Mathf.InverseLerp(deadAngle, maxAngle, magnitude);
            return Mathf.Clamp01(normalized) * Mathf.Sign(angle) * sign;
        }

        void UpdateCrosshairUI()
        {
            if (crosshairUI == null || playerViewCamera == null)
                return;

            var worldPoint = GetCrosshairWorldPoint();
            crosshairUI.Show(worldPoint, playerViewCamera);
        }

        void FireHitscan()
        {
            var ray = targetingViewCamera.ViewportPointToRay(new Vector3(crosshairViewportPosition.x, crosshairViewportPosition.y, 0f));
            if (showDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, debugRayColor, fireInterval);

            var didHit = Physics.Raycast(ray, out var hit, maxDistance, hitLayers, triggerInteraction);
            var hitDistance = didHit ? hit.distance : maxDistance;
            PlayFireAudio();
            PlayHitscanSmoke(ray, hitDistance);

            if (!didHit)
                return;

            DealDamage(hit.collider.gameObject);
        }

        void DealDamage(GameObject hitObject)
        {
            if (hitObject == null || damage <= 0f)
                return;

            var attackReceiver = hitObject.GetComponentInParent<IAttackDamageReceiver>();
            if (attackReceiver != null)
            {
                attackReceiver.TakeDamage(damage, gameObject);
                return;
            }

            var damageable = hitObject.GetComponentInParent<Damageable>();
            if (damageable != null)
                damageable.TakeDamage(damage, gameObject);
        }

        void PlayFireAudio()
        {
            if (fireAudioClip == null)
                return;

            if (fireAudioSource == null)
                fireAudioSource = GetComponent<AudioSource>();

            if (fireAudioSource == null)
                fireAudioSource = gameObject.AddComponent<AudioSource>();

            fireAudioSource.playOnAwake = false;
            fireAudioSource.spatialBlend = 1f;
            fireAudioSource.pitch = fireAudioPitch + Random.Range(-fireAudioPitchJitter, fireAudioPitchJitter);
            fireAudioSource.PlayOneShot(fireAudioClip, fireAudioVolume);
        }

        void PlayHitscanSmoke(Ray targetingRay, float hitDistance)
        {
            if (!showHitscanSmoke)
                return;

            var sourceObject = muzzleGasPrefab != null
                ? muzzleGasPrefab
                : muzzleGasEffect != null
                    ? muzzleGasEffect.gameObject
                    : null;

            if (sourceObject == null)
                return;

            var targetingEnd = targetingRay.origin + targetingRay.direction * hitDistance;
            var visualStart = muzzlePoint != null ? muzzlePoint.position : targetingRay.origin;
            var visualDelta = targetingEnd - visualStart;
            var visualDistance = visualDelta.magnitude;
            if (visualDistance <= Mathf.Epsilon)
                return;

            var visualDirection = visualDelta / visualDistance;
            var clampedDistance = Mathf.Min(visualDistance, smokeMaxVisualDistance);
            var startOffset = Mathf.Min(smokeStartOffset, clampedDistance * 0.5f);
            var traceStart = visualStart + visualDirection * startOffset;
            var traceDistance = Mathf.Max(0.01f, clampedDistance - startOffset);
            var effect = ResolveHitscanSmokeEffect(sourceObject, traceStart, visualDirection);
            if (effect == null)
                return;

            if (!effect.isPlaying)
                effect.Play(true);

            var spacing = Mathf.Max(0.001f, smokePuffSpacing);
            var spacingPuffCount = Mathf.CeilToInt(traceDistance / spacing) + 1;
            var puffCount = Mathf.Clamp(
                Mathf.Max(smokePuffCount, spacingPuffCount),
                1,
                Mathf.Max(1, smokeMaxPuffCount));

            for (var i = 0; i < puffCount; i++)
            {
                var t = puffCount == 1 ? 0.5f : i / (puffCount - 1f);
                var position = traceStart + visualDirection * (traceDistance * t);
                var offset = Vector3.ProjectOnPlane(Random.insideUnitSphere, visualDirection) * smokeSpreadRadius;
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = position + offset,
                    startLifetime = smokeLifeTime,
                    startSize = smokeStartSize,
                    startColor = smokeColor
                };

                if (useScriptedSmokeVelocity)
                    emitParams.velocity = visualDirection * smokeForwardDrift + Random.insideUnitSphere * smokeTurbulence;

                effect.Emit(emitParams, smokeParticlesPerPuff);
            }
        }

        ParticleSystem ResolveHitscanSmokeEffect(GameObject sourceObject, Vector3 position, Vector3 direction)
        {
            if (hitscanSmokeEffectInstance != null)
            {
                EnsureHitscanSmokeConfigured();
                return hitscanSmokeEffectInstance;
            }

            if (muzzleGasEffect != null && muzzleGasEffect.gameObject.scene.IsValid())
            {
                hitscanSmokeEffectInstance = muzzleGasEffect;
                hitscanSmokeObjectInstance = muzzleGasEffect.gameObject;
                EnsureHitscanSmokeConfigured();
                return hitscanSmokeEffectInstance;
            }

            if (sourceObject == null)
                return null;

            var smokeObject = Instantiate(sourceObject, position, Quaternion.LookRotation(direction, Vector3.up), transform);
            smokeObject.name = $"{sourceObject.name}_Runtime";
            smokeObject.transform.localScale = Vector3.one * smokeVisualScale;
            hitscanSmokeObjectInstance = smokeObject;
            hitscanSmokeEffectInstance = smokeObject.GetComponentInChildren<ParticleSystem>();
            ownsHitscanSmokeObjectInstance = true;
            EnsureHitscanSmokeConfigured();
            return hitscanSmokeEffectInstance;
        }

        void EnsureHitscanSmokeConfigured()
        {
            if (hitscanSmokeConfigured || hitscanSmokeEffectInstance == null)
                return;

            hitscanSmokeEffectInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ConfigureHitscanSmoke(hitscanSmokeEffectInstance);
            hitscanSmokeConfigured = true;
        }

        void ConfigureHitscanSmoke(ParticleSystem effect)
        {
            var main = effect.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = Mathf.Max(main.maxParticles, CalculateSmokeParticleCapacity());

            var emission = effect.emission;
            emission.enabled = false;
        }

        int CalculateSmokeParticleCapacity()
        {
            var particlesPerShot = Mathf.Max(1, smokeMaxPuffCount) * Mathf.Max(1, smokeParticlesPerPuff);
            var overlappingShotCount = Mathf.CeilToInt(smokeLifeTime / Mathf.Max(0.01f, fireInterval)) + 1;
            return Mathf.Max(particlesPerShot, particlesPerShot * Mathf.Max(1, overlappingShotCount));
        }

        Vector3 GetCrosshairWorldPoint()
        {
            var aimDistance = Mathf.Min(crosshairPlaneDistance, maxDistance);
            var ray = targetingViewCamera.ViewportPointToRay(new Vector3(crosshairViewportPosition.x, crosshairViewportPosition.y, 0f));
            return ray.origin + ray.direction * aimDistance;
        }

        Vector2 ClampViewport(Vector2 value)
        {
            var margin = Mathf.Clamp(viewportMargin, 0f, 0.45f);
            return new Vector2(
                Mathf.Clamp(value.x, margin, 1f - margin),
                Mathf.Clamp(value.y, margin, 1f - margin));
        }

        XRController GetLeftController()
        {
            if (cachedLeftController != null && cachedLeftController.added && IsLeftHandController(cachedLeftController))
                return cachedLeftController;

            cachedLeftController = null;
            foreach (var device in InputSystem.devices)
            {
                if (device is not XRController controller || !IsLeftHandController(controller))
                    continue;

                cachedLeftController = controller;
                return cachedLeftController;
            }

            return null;
        }

        bool IsFirePressed()
        {
            var action = fireAction != null ? fireAction.action : null;
            if (action != null)
                return action.ReadValue<float>() >= fireThreshold;

            var leftController = GetLeftController();
            if (leftController != null)
            {
                var trigger = leftController.TryGetChildControl<AxisControl>("trigger");
                if (trigger != null)
                    return trigger.ReadValue() >= fireThreshold;
            }

            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.fKey.isPressed;
        }

        static bool IsLeftHandController(InputDevice device)
        {
            foreach (var usage in device.usages)
            {
                if (usage == CommonUsages.LeftHand)
                    return true;
            }

            return false;
        }

        static Vector3 GetEulerDelta(Vector3 neutralEuler, Vector3 currentEuler)
        {
            return new Vector3(
                Mathf.DeltaAngle(neutralEuler.x, currentEuler.x),
                Mathf.DeltaAngle(neutralEuler.y, currentEuler.y),
                Mathf.DeltaAngle(neutralEuler.z, currentEuler.z));
        }

        static bool IsValidRotation(Quaternion rotation)
        {
            return IsFinite(rotation.x)
                && IsFinite(rotation.y)
                && IsFinite(rotation.z)
                && IsFinite(rotation.w)
                && rotation != new Quaternion(0f, 0f, 0f, 0f);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class MachineGunCrosshairUI : MonoBehaviour
    {
        [SerializeField] Color crosshairColor = new Color(0.2f, 1f, 0.85f, 0.9f);
        [SerializeField, Min(1f)] float lineLength = 22f;
        [SerializeField, Min(0.5f)] float lineThickness = 3f;
        [SerializeField, Min(0f)] float centerGap = 7f;
        [SerializeField] int sortingOrder = 90;
        [SerializeField, Min(0.01f)] float planeDistance = 0.45f;

        Canvas canvas;
        RectTransform root;
        Image up;
        Image down;
        Image left;
        Image right;
        Sprite lineSprite;

        public void Show(Vector3 worldPoint, Camera displayCamera)
        {
            if (displayCamera == null)
            {
                Hide();
                return;
            }

            var screenPoint = displayCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f)
            {
                Hide();
                return;
            }

            EnsureUi(displayCamera);
            root.gameObject.SetActive(true);
            root.anchoredPosition = new Vector2(screenPoint.x - Screen.width * 0.5f, screenPoint.y - Screen.height * 0.5f);
            ApplyLineLayout();
            ApplyColor();
        }

        public void Hide()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        void EnsureUi(Camera displayCamera)
        {
            if (root != null)
            {
                canvas.worldCamera = displayCamera;
                canvas.sortingOrder = sortingOrder;
                canvas.planeDistance = planeDistance;
                return;
            }

            lineSprite = CreateLineSprite();

            var canvasObject = new GameObject("Machine Gun Crosshair Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = displayCamera;
            canvas.planeDistance = planeDistance;
            canvas.sortingOrder = sortingOrder;
            canvas.overrideSorting = true;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var rootObject = new GameObject("Machine Gun Crosshair", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            up = CreateLine("Up", root);
            down = CreateLine("Down", root);
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

        void ApplyLineLayout()
        {
            SetVertical(up.rectTransform, centerGap + lineLength * 0.5f);
            SetVertical(down.rectTransform, -centerGap - lineLength * 0.5f);
            SetHorizontal(left.rectTransform, -centerGap - lineLength * 0.5f);
            SetHorizontal(right.rectTransform, centerGap + lineLength * 0.5f);
        }

        void SetVertical(RectTransform rectTransform, float y)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, y);
            rectTransform.sizeDelta = new Vector2(lineThickness, lineLength);
        }

        void SetHorizontal(RectTransform rectTransform, float x)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(x, 0f);
            rectTransform.sizeDelta = new Vector2(lineLength, lineThickness);
        }

        void ApplyColor()
        {
            up.color = crosshairColor;
            down.color = crosshairColor;
            left.color = crosshairColor;
            right.color = crosshairColor;
        }

        static Sprite CreateLineSprite()
        {
            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
