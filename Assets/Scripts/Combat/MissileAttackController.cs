using HomingMissile;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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

        [Header("Lock On")]
        [SerializeField] bool requireLockOn = true;
        [SerializeField] float lockOnTime = 1.5f;
        [SerializeField] float lockOnMaxDistance = 150f;
        [SerializeField, Range(1f, 90f)] float lockOnAngle = 20f;
        [SerializeField] LayerMask lockOnLayers = ~0;
        [SerializeField] bool requireDamageReceiver = true;
        [SerializeField] bool useTargetingCameraForLockOn = true;
        [SerializeField] Transform lockOnReferencePoint;
        [SerializeField] Camera targetingCamera;
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

        void Awake()
        {
            if (lockOnReticle == null)
                lockOnReticle = GetComponent<MissileLockOnReticleUI>();

            if (requireLockOn && lockOnReticle == null)
                lockOnReticle = gameObject.AddComponent<MissileLockOnReticleUI>();

            ApplyLockOnReticleSettings();
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
            ResetLockOn();
        }

        void Update()
        {
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
                lockOnReticle.Show(currentLockOnTarget, progress, progress >= 1f, GetTargetingCamera());

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
                lockOnReticle.Show(pendingFireTarget, 1f, true, GetTargetingCamera());

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

            var minDot = Mathf.Cos(lockOnAngle * Mathf.Deg2Rad);
            var dot = Vector3.Dot(referencePoint.forward, toTarget / distance);
            return dot >= minDot;
        }

        Transform FindBestLockOnTarget(Transform launchPoint)
        {
            var origin = launchPoint.position;
            var forward = launchPoint.forward;
            var minDot = Mathf.Cos(lockOnAngle * Mathf.Deg2Rad);
            var hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                lockOnMaxDistance,
                lockOnCandidates,
                lockOnLayers,
                QueryTriggerInteraction.Ignore);

            Transform bestTarget = null;
            var bestDot = minDot;
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

                var dot = Vector3.Dot(forward, toTarget / distance);
                if (dot < minDot)
                    continue;

                if (dot > bestDot || (Mathf.Approximately(dot, bestDot) && distance < bestDistance))
                {
                    bestTarget = candidate;
                    bestDot = dot;
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

        Camera GetTargetingCamera()
        {
            if (targetingCamera != null)
                return targetingCamera;

            targetingCamera = Camera.main;
            return targetingCamera;
        }

        Transform GetLockOnReferencePoint()
        {
            if (lockOnReferencePoint != null)
                return lockOnReferencePoint;

            if (useTargetingCameraForLockOn)
            {
                var camera = GetTargetingCamera();
                if (camera != null)
                    return camera.transform;
            }

            return muzzlePoint != null ? muzzlePoint : transform;
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

            DrawLockOnCone(GetLockOnReferencePoint());
            DrawCurrentLockOnTarget();
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
}
