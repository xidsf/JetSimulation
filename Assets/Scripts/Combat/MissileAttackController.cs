using HomingMissile;
using JetSimulation.EnemySystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace JetSimulation.Combat
{
    public sealed class MissileAttackController : MonoBehaviour
    {
        enum LockOnState
        {
            Idle,
            Acquiring,
            LockedPendingFire,
            Releasing
        }

        [Header("Launch")]
        [SerializeField] GameObject missilePrefab;
        [SerializeField] Transform muzzlePoint;
        [SerializeField] Transform defaultTarget;

        [Header("Input")]
        [SerializeField] InputActionReference fireAction;
        [SerializeField, Range(0.01f, 1f)] float triggerThreshold = 0.5f;

        [Header("Tuning")]
        [SerializeField] bool attackEnabled = true;
        [SerializeField] float cooldown = 0.5f;
        [SerializeField] float damage = 35f;
        [SerializeField] float simpleLaunchSpeed = 80f;
        [SerializeField] float projectileLifeTime = 8f;
        [SerializeField] float temporaryTargetDistance = 120f;

        [Header("Targeting View")]
        [FormerlySerializedAs("lockOnViewCamera")]
        [FormerlySerializedAs("weaponAimCamera")]
        [SerializeField, InspectorName("Targeting Camera")] Camera targetingViewCamera;
        [FormerlySerializedAs("requireTargetInsideLockOnCameraView")]
        [FormerlySerializedAs("requireTargetInsideWeaponAimView")]
        [SerializeField] bool requireTargetInsideTargetingView = true;
        [FormerlySerializedAs("showWeaponAimArea")]
        [SerializeField] bool showTargetingArea = true;
        [FormerlySerializedAs("weaponAimAreaUI")]
        [SerializeField, InspectorName("Targeting Area UI")] WeaponAimAreaUI targetingAreaUI;

        [Header("Lock On")]
        [SerializeField] bool requireLockOn = true;
        [SerializeField] float lockOnTime = 1.5f;
        [SerializeField] float lockOnMaxDistance = 150f;
        [SerializeField, Range(1f, 90f)] float lockOnAngle = 20f;
        [SerializeField] LayerMask lockOnLayers = ~0;
        [SerializeField, InspectorName("Require Damageable Target")] bool requireDamageReceiver = true;
        [SerializeField] Transform lockOnReferencePoint;
        [FormerlySerializedAs("targetingCamera")]
        [SerializeField, InspectorName("Player View Camera")] Camera playerViewCamera;
        [SerializeField] MissileLockOnReticleUI lockOnReticle;
        [SerializeField, Min(1f)] float lockOnInitialReticleSize = 220f;
        [SerializeField, Min(0f)] float lockOnFireDelay = 1f;
        [SerializeField, Min(0.01f)] float lockOnReleaseDuration = 0.25f;

        [Header("Debug")]
        [SerializeField] bool showLockOnGizmos = true;
        [SerializeField] Color lockOnGizmoColor = new Color(1f, 0.85f, 0.1f, 0.35f);
        [SerializeField] Color currentTargetGizmoColor = new Color(1f, 0.1f, 0.05f, 0.9f);

        [Header("Events")]
        [SerializeField] UnityEvent<GameObject> missileFired;
        [SerializeField] UnityEvent<bool> attackEnabledChanged;
        [SerializeField] UnityEvent<Transform> lockOnTargetChanged;
        [SerializeField] UnityEvent<Transform> lockOnCompleted;

        float nextFireTime;
        bool wasTriggerPressed;
        bool firedDuringCurrentHold;
        bool fireInputConsumed;
        float lockOnTimer;
        float lockOnFireDelayTimer;
        float lockOnReleaseTimer;
        bool lockOnCompleteNotified;
        bool inputReleasedAfterLockComplete;
        bool queuedNextLockAfterPendingFire;
        LockOnState lockOnState;
        Transform currentLockOnTarget;
        Transform pendingFireTarget;
        readonly Collider[] lockOnCandidates = new Collider[64];

        public bool AttackEnabled => attackEnabled;
        public bool CanAttack => attackEnabled && missilePrefab != null && Time.time >= nextFireTime;
        public GameObject LastFiredMissile { get; private set; }
        public Transform CurrentLockOnTarget => currentLockOnTarget;
        public float LockOnProgress => lockOnTime <= 0f ? 1f : Mathf.Clamp01(lockOnTimer / lockOnTime);
        public Camera TargetingCamera => targetingViewCamera;
        public Camera PlayerViewCamera => GetPlayerViewCamera();

        void Awake()
        {
            if (lockOnReticle == null)
                lockOnReticle = GetComponent<MissileLockOnReticleUI>();

            if (requireLockOn && lockOnReticle == null)
                lockOnReticle = gameObject.AddComponent<MissileLockOnReticleUI>();

            if (targetingAreaUI == null)
                targetingAreaUI = GetComponent<WeaponAimAreaUI>();

            if (showTargetingArea && targetingAreaUI == null)
                targetingAreaUI = gameObject.AddComponent<WeaponAimAreaUI>();

            ApplyLockOnReticleSettings();
            UpdateTargetingArea();
        }

        public void SetAttackEnabled(bool enabled)
        {
            if (attackEnabled == enabled)
                return;

            attackEnabled = enabled;
            attackEnabledChanged?.Invoke(attackEnabled);
        }

        public void SetTarget(Transform target)
        {
            defaultTarget = target;
        }

        public void SetMuzzlePoint(Transform muzzle)
        {
            muzzlePoint = muzzle;
        }

        public bool TryFire()
        {
            return TryFireAt(defaultTarget);
        }

        public bool TryFireAt(Transform target)
        {
            if (!CanAttack)
                return false;

            var launchPoint = muzzlePoint != null ? muzzlePoint : transform;
            var targetObject = ResolveTarget(target, launchPoint);
            var missileObject = Instantiate(missilePrefab, launchPoint.position, launchPoint.rotation);
            var homing = ConfigureHomingMissile(missileObject, targetObject, launchPoint);

            ConfigureFallbackMotion(missileObject, homing, launchPoint);
            ConfigurePayload(missileObject, homing);

            LastFiredMissile = missileObject;
            nextFireTime = Time.time + cooldown;
            missileFired?.Invoke(missileObject);
            return true;
        }

        void OnEnable()
        {
            fireAction?.action?.Enable();
        }

        void OnDisable()
        {
            fireAction?.action?.Disable();
            wasTriggerPressed = false;
            fireInputConsumed = false;
            if (targetingAreaUI != null)
                targetingAreaUI.Hide();
            ResetLockOn();
        }

        void Update()
        {
            UpdateTargetingArea();

            var action = fireAction != null ? fireAction.action : null;
            if (action == null)
                return;

            var isTriggerPressed = action.ReadValue<float>() >= triggerThreshold;
            if (requireLockOn)
            {
                UpdateLockOn(isTriggerPressed);
                wasTriggerPressed = isTriggerPressed;
                return;
            }

            if (isTriggerPressed && !wasTriggerPressed)
                TryFire();

            wasTriggerPressed = isTriggerPressed;
        }

        void UpdateLockOn(bool isTriggerPressed)
        {
            ApplyLockOnReticleSettings();

            if (lockOnState == LockOnState.LockedPendingFire)
            {
                UpdateLockedPendingFire(isTriggerPressed);
                return;
            }

            if (lockOnState == LockOnState.Releasing)
            {
                UpdateLockOnRelease();
                return;
            }

            if (!isTriggerPressed)
            {
                fireInputConsumed = false;
                if (currentLockOnTarget != null || lockOnTimer > 0f)
                    BeginLockOnRelease();
                else
                    ResetLockOn();

                return;
            }

            if (!attackEnabled)
            {
                BeginLockOnRelease();
                return;
            }

            if (fireInputConsumed)
            {
                if (lockOnReticle != null)
                    lockOnReticle.Hide();
                return;
            }

            var referencePoint = GetLockOnReferencePoint();
            var target = GetLockOnTarget(referencePoint);
            if (target == null)
            {
                if (currentLockOnTarget != null || lockOnTimer > 0f)
                    BeginLockOnRelease();
                else
                    ResetLockOn();

                return;
            }

            if (currentLockOnTarget != target)
            {
                currentLockOnTarget = target;
                lockOnTimer = 0f;
                lockOnFireDelayTimer = 0f;
                lockOnReleaseTimer = 0f;
                firedDuringCurrentHold = false;
                lockOnCompleteNotified = false;
                lockOnState = LockOnState.Acquiring;
                lockOnTargetChanged?.Invoke(currentLockOnTarget);
            }

            lockOnTimer += Time.deltaTime;
            var progress = LockOnProgress;
            if (lockOnReticle != null)
                lockOnReticle.Show(currentLockOnTarget, progress, progress >= 1f, GetPlayerViewCamera());

            if (progress < 1f || firedDuringCurrentHold)
            {
                lockOnFireDelayTimer = 0f;
                return;
            }

            BeginLockedPendingFire();
        }

        void UpdateLockedPendingFire(bool isTriggerPressed)
        {
            if (!isTriggerPressed)
                inputReleasedAfterLockComplete = true;

            if (inputReleasedAfterLockComplete && isTriggerPressed && !wasTriggerPressed)
                queuedNextLockAfterPendingFire = true;

            if (lockOnReticle != null && pendingFireTarget != null)
                lockOnReticle.Show(pendingFireTarget, 1f, true, GetPlayerViewCamera());

            if (!attackEnabled)
                return;

            lockOnFireDelayTimer += Time.deltaTime;
            if (lockOnFireDelayTimer < lockOnFireDelay)
                return;

            var targetToFire = pendingFireTarget;
            if (TryFireAt(targetToFire))
            {
                firedDuringCurrentHold = true;
                fireInputConsumed = isTriggerPressed && !queuedNextLockAfterPendingFire;
                ClearLockOnAfterFire();
            }
        }

        void UpdateLockOnRelease()
        {
            lockOnReleaseTimer += Time.deltaTime;

            if (lockOnReticle != null)
                lockOnReticle.ShowRelease(lockOnReleaseTimer / lockOnReleaseDuration);

            if (lockOnReleaseTimer >= lockOnReleaseDuration)
                ResetLockOn();
        }

        void BeginLockedPendingFire()
        {
            if (!lockOnCompleteNotified)
            {
                lockOnCompleteNotified = true;
                lockOnCompleted?.Invoke(currentLockOnTarget);
            }

            pendingFireTarget = currentLockOnTarget;
            lockOnFireDelayTimer = 0f;
            inputReleasedAfterLockComplete = false;
            queuedNextLockAfterPendingFire = false;
            lockOnState = LockOnState.LockedPendingFire;
        }

        Transform GetLockOnTarget(Transform referencePoint)
        {
            if (currentLockOnTarget != null && IsLockOnTargetStillValid(currentLockOnTarget, referencePoint))
                return currentLockOnTarget;

            return FindBestLockOnTarget(referencePoint);
        }

        bool IsLockOnTargetStillValid(Transform target, Transform referencePoint)
        {
            if (target == null || referencePoint == null || !target.gameObject.activeInHierarchy)
                return false;

            if (IsSelf(target))
                return false;

            var toTarget = target.position - referencePoint.position;
            var distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon || distance > lockOnMaxDistance)
                return false;

            if (UseTargetingCameraView())
                return IsInsideTargetingView(target);

            var minDot = Mathf.Cos(lockOnAngle * Mathf.Deg2Rad);
            var dot = Vector3.Dot(referencePoint.forward, toTarget / distance);
            return dot >= minDot;
        }

        Transform FindBestLockOnTarget(Transform launchPoint)
        {
            var origin = launchPoint.position;
            var forward = launchPoint.forward;
            var minDot = Mathf.Cos(lockOnAngle * Mathf.Deg2Rad);
            var useTargetingCameraView = UseTargetingCameraView();
            var hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                lockOnMaxDistance,
                lockOnCandidates,
                lockOnLayers,
            QueryTriggerInteraction.Ignore);

            Transform bestTarget = null;
            var bestDot = useTargetingCameraView ? float.MinValue : minDot;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var candidate = ResolveLockOnTarget(lockOnCandidates[i]);
                if (candidate == null || IsSelf(candidate))
                    continue;

                var toTarget = candidate.position - origin;
                var distance = toTarget.magnitude;
                if (distance <= Mathf.Epsilon || distance > lockOnMaxDistance)
                    continue;

                var direction = toTarget / distance;
                var dot = Vector3.Dot(forward, direction);
                if (useTargetingCameraView)
                {
                    if (!IsInsideTargetingView(candidate))
                        continue;
                }
                else if (dot < minDot)
                {
                    continue;
                }

                var score = GetLockOnAimScore(candidate, dot);
                if (score > bestDot || (Mathf.Approximately(score, bestDot) && distance < bestDistance))
                {
                    bestTarget = candidate;
                    bestDot = score;
                    bestDistance = distance;
                }
            }

            return bestTarget;
        }

        Transform ResolveLockOnTarget(Collider candidate)
        {
            if (candidate == null)
                return null;

            if (!requireDamageReceiver)
                return candidate.attachedRigidbody != null ? candidate.attachedRigidbody.transform : candidate.transform;

            var behaviours = candidate.GetComponentsInParent<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is Damageable)
                    return behaviours[i].transform;
            }

            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAttackDamageReceiver)
                    return behaviours[i].transform;
            }

            return null;
        }

        bool IsSelf(Transform candidate)
        {
            return candidate == transform ||
                   candidate.IsChildOf(transform) ||
                   transform.IsChildOf(candidate);
        }

        Camera GetPlayerViewCamera()
        {
            if (playerViewCamera != null)
                return playerViewCamera;

            playerViewCamera = Camera.main;
            return playerViewCamera;
        }

        Transform GetLockOnReferencePoint()
        {
            if (lockOnReferencePoint != null)
                return lockOnReferencePoint;

            if (targetingViewCamera != null)
                return targetingViewCamera.transform;

            return muzzlePoint != null ? muzzlePoint : transform;
        }

        bool UseTargetingCameraView()
        {
            return requireTargetInsideTargetingView && targetingViewCamera != null;
        }

        bool IsInsideTargetingView(Transform target)
        {
            if (!UseTargetingCameraView())
                return true;

            return TryGetBestTargetingViewportPoint(target, out _);
        }

        float GetLockOnAimScore(Transform target, float dot)
        {
            if (UseTargetingCameraView() && TryGetBestTargetingViewportPoint(target, out var viewportPoint))
            {
                var viewportOffset = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
                return 1f - viewportOffset.sqrMagnitude;
            }

            return dot;
        }

        bool TryGetBestTargetingViewportPoint(Transform target, out Vector3 bestViewportPoint)
        {
            bestViewportPoint = default;
            if (target == null || targetingViewCamera == null)
                return false;

            var bounds = GetTargetBounds(target);
            var bestCenterDistance = float.MaxValue;
            var hasPointInsideView = false;

            CheckViewportPoint(bounds.center, ref bestViewportPoint, ref bestCenterDistance, ref hasPointInsideView);

            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            for (var z = 0; z <= 1; z++)
            {
                var worldPoint = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);

                CheckViewportPoint(worldPoint, ref bestViewportPoint, ref bestCenterDistance, ref hasPointInsideView);
            }

            return hasPointInsideView;
        }

        void CheckViewportPoint(
            Vector3 worldPoint,
            ref Vector3 bestViewportPoint,
            ref float bestCenterDistance,
            ref bool hasPointInsideView)
        {
            var viewportPoint = targetingViewCamera.WorldToViewportPoint(worldPoint);
            if (viewportPoint.z <= 0f ||
                viewportPoint.x < 0f ||
                viewportPoint.x > 1f ||
                viewportPoint.y < 0f ||
                viewportPoint.y > 1f)
            {
                return;
            }

            var centerDistance = (new Vector2(viewportPoint.x, viewportPoint.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;
            if (!hasPointInsideView || centerDistance < bestCenterDistance)
            {
                bestViewportPoint = viewportPoint;
                bestCenterDistance = centerDistance;
            }

            hasPointInsideView = true;
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

        void ResetLockOn()
        {
            if (lockOnReticle != null)
                lockOnReticle.Hide();

            if (currentLockOnTarget != null)
                lockOnTargetChanged?.Invoke(null);

            currentLockOnTarget = null;
            pendingFireTarget = null;
            lockOnTimer = 0f;
            lockOnFireDelayTimer = 0f;
            lockOnReleaseTimer = 0f;
            firedDuringCurrentHold = false;
            lockOnCompleteNotified = false;
            inputReleasedAfterLockComplete = false;
            queuedNextLockAfterPendingFire = false;
            lockOnState = LockOnState.Idle;
        }

        void BeginLockOnRelease()
        {
            if (lockOnReticle == null || !lockOnReticle.BeginRelease())
            {
                ResetLockOn();
                return;
            }

            if (currentLockOnTarget != null)
                lockOnTargetChanged?.Invoke(null);

            currentLockOnTarget = null;
            pendingFireTarget = null;
            lockOnTimer = 0f;
            lockOnFireDelayTimer = 0f;
            lockOnReleaseTimer = 0f;
            firedDuringCurrentHold = false;
            lockOnCompleteNotified = false;
            inputReleasedAfterLockComplete = false;
            queuedNextLockAfterPendingFire = false;
            lockOnState = LockOnState.Releasing;
        }

        void ClearLockOnAfterFire()
        {
            if (lockOnReticle != null)
                lockOnReticle.Hide();

            if (currentLockOnTarget != null)
                lockOnTargetChanged?.Invoke(null);

            currentLockOnTarget = null;
            pendingFireTarget = null;
            lockOnTimer = 0f;
            lockOnFireDelayTimer = 0f;
            lockOnReleaseTimer = 0f;
            lockOnCompleteNotified = false;
            inputReleasedAfterLockComplete = false;
            queuedNextLockAfterPendingFire = false;
            lockOnState = LockOnState.Idle;
        }

        void ApplyLockOnReticleSettings()
        {
            if (lockOnReticle != null)
                lockOnReticle.SetInitialSize(lockOnInitialReticleSize);
        }

        void UpdateTargetingArea()
        {
            if (targetingAreaUI == null)
                return;

            if (!showTargetingArea || targetingViewCamera == null)
            {
                targetingAreaUI.Hide();
                return;
            }

            targetingAreaUI.Show(targetingViewCamera, GetPlayerViewCamera(), lockOnMaxDistance);
        }

        GameObject ResolveTarget(Transform requestedTarget, Transform launchPoint)
        {
            if (requestedTarget != null && requestedTarget.gameObject.activeInHierarchy)
                return requestedTarget.gameObject;

            var targetObject = new GameObject("Temporary Missile Target");
            targetObject.transform.position = launchPoint.position + launchPoint.forward * temporaryTargetDistance;
            targetObject.AddComponent<TemporaryMissileTarget>().Initialize(projectileLifeTime + 1f);
            return targetObject;
        }

        homing_missile ConfigureHomingMissile(GameObject missileObject, GameObject targetObject, Transform launchPoint)
        {
            if (!missileObject.TryGetComponent(out homing_missile homing))
                return null;

            homing.target = targetObject;
            homing.shooter = launchPoint.gameObject;
            homing.damage = Mathf.RoundToInt(damage);

            if (homing.targetpointer != null &&
                homing.targetpointer.TryGetComponent(out homing_missile_pointer pointer))
            {
                pointer.target = targetObject;
            }

            homing.usemissile();
            return homing;
        }

        void ConfigureFallbackMotion(GameObject missileObject, homing_missile homing, Transform launchPoint)
        {
            if (homing != null)
                return;

            if (!missileObject.TryGetComponent(out Rigidbody body))
                body = missileObject.AddComponent<Rigidbody>();

            if (!missileObject.TryGetComponent(out SimpleMissileMover mover))
                mover = missileObject.AddComponent<SimpleMissileMover>();

            mover.Launch(launchPoint.forward, simpleLaunchSpeed);
        }

        void ConfigurePayload(GameObject missileObject, homing_missile homing)
        {
            if (!missileObject.TryGetComponent(out MissilePayload payload))
                payload = missileObject.AddComponent<MissilePayload>();

            payload.Initialize(damage, gameObject, projectileLifeTime, homing);
        }

        void OnDrawGizmosSelected()
        {
            if (!showLockOnGizmos)
                return;

            if (UseTargetingCameraView())
                DrawTargetingCameraFrustum();
            else
                DrawLockOnCone(GetLockOnReferencePoint());

            DrawCurrentLockOnTarget();
        }

        void DrawTargetingCameraFrustum()
        {
            if (targetingViewCamera == null)
                return;

            var oldMatrix = Gizmos.matrix;
            Gizmos.color = lockOnGizmoColor;
            Gizmos.matrix = targetingViewCamera.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(
                Vector3.zero,
                targetingViewCamera.fieldOfView,
                lockOnMaxDistance,
                Mathf.Max(0.01f, targetingViewCamera.nearClipPlane),
                targetingViewCamera.aspect);
            Gizmos.matrix = oldMatrix;
        }

        void DrawLockOnCone(Transform launchPoint)
        {
            var origin = launchPoint.position;
            var forward = launchPoint.forward.normalized;
            var up = launchPoint.up.normalized;
            var right = launchPoint.right.normalized;
            var radius = Mathf.Tan(lockOnAngle * Mathf.Deg2Rad) * lockOnMaxDistance;
            var center = origin + forward * lockOnMaxDistance;

            Gizmos.color = lockOnGizmoColor;
            Gizmos.DrawLine(origin, center);

            const int segments = 48;
            var previous = center + right * radius;
            for (var i = 1; i <= segments; i++)
            {
                var radians = i / (float)segments * Mathf.PI * 2f;
                var point = center + (right * Mathf.Cos(radians) + up * Mathf.Sin(radians)) * radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            Gizmos.DrawLine(origin, center + right * radius);
            Gizmos.DrawLine(origin, center - right * radius);
            Gizmos.DrawLine(origin, center + up * radius);
            Gizmos.DrawLine(origin, center - up * radius);
        }

        void DrawCurrentLockOnTarget()
        {
            if (currentLockOnTarget == null)
                return;

            Gizmos.color = currentTargetGizmoColor;
            Gizmos.DrawLine(GetLockOnReferencePoint().position, currentLockOnTarget.position);
            Gizmos.DrawWireSphere(currentLockOnTarget.position, 1.5f);
        }
    }

    public sealed class WeaponAimAreaUI : MonoBehaviour
    {
        [SerializeField] Color lineColor = new Color(0.1f, 0.9f, 1f, 0.55f);
        [SerializeField, Min(0.5f)] float lineThickness = 3f;
        [SerializeField] int sortingOrder = 20;
        [SerializeField, Min(0.01f)] float planeDistance = 0.5f;

        Canvas canvas;
        RectTransform root;
        Image[] edges;
        Sprite lineSprite;

        public void Show(Camera targetingCamera, Camera playerViewCamera, float distance)
        {
            if (targetingCamera == null || playerViewCamera == null || distance <= 0f)
            {
                Hide();
                return;
            }

            EnsureUi(playerViewCamera);

            if (!TryGetProjectedTargetingCorners(targetingCamera, playerViewCamera, distance, out var corners))
            {
                Hide();
                return;
            }

            root.gameObject.SetActive(true);
            for (var i = 0; i < edges.Length; i++)
                SetEdge(edges[i].rectTransform, corners[i], corners[(i + 1) % corners.Length]);
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

            var canvasObject = new GameObject("Targeting Area Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = displayCamera;
            canvas.planeDistance = planeDistance;
            canvas.sortingOrder = sortingOrder;
            canvas.overrideSorting = true;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var rootObject = new GameObject("Targeting Area", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            edges = new Image[4];
            for (var i = 0; i < edges.Length; i++)
                edges[i] = CreateLine($"Edge {i + 1}", root);

            Hide();
        }

        Image CreateLine(string lineName, Transform parent)
        {
            var lineObject = new GameObject(lineName, typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(parent, false);
            var image = lineObject.GetComponent<Image>();
            image.sprite = lineSprite;
            image.color = lineColor;
            image.raycastTarget = false;
            return image;
        }

        void SetEdge(RectTransform rectTransform, Vector2 start, Vector2 end)
        {
            var delta = end - start;
            var length = delta.magnitude;
            var center = (start + end) * 0.5f;

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = center - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            rectTransform.sizeDelta = new Vector2(length, lineThickness);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        bool TryGetProjectedTargetingCorners(
            Camera targetingCamera,
            Camera playerViewCamera,
            float distance,
            out Vector2[] screenCorners)
        {
            screenCorners = new Vector2[4];

            var aimTransform = targetingCamera.transform;
            var aimDistance = Mathf.Min(distance, Mathf.Max(targetingCamera.nearClipPlane, targetingCamera.farClipPlane));
            var halfHeight = Mathf.Tan(targetingCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * aimDistance;
            var halfWidth = halfHeight * targetingCamera.aspect;
            var center = aimTransform.position + aimTransform.forward * aimDistance;

            var worldCorners = new[]
            {
                center + aimTransform.up * halfHeight - aimTransform.right * halfWidth,
                center + aimTransform.up * halfHeight + aimTransform.right * halfWidth,
                center - aimTransform.up * halfHeight + aimTransform.right * halfWidth,
                center - aimTransform.up * halfHeight - aimTransform.right * halfWidth
            };

            for (var i = 0; i < worldCorners.Length; i++)
            {
                var screenPoint = playerViewCamera.WorldToScreenPoint(worldCorners[i]);
                if (screenPoint.z <= 0f)
                    return false;

                screenCorners[i] = screenPoint;
            }

            return true;
        }

        static Sprite CreateLineSprite()
        {
            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
