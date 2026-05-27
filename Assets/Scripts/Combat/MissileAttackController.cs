using HomingMissile;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace JetSimulation.Combat
{
    public sealed class MissileAttackController : MonoBehaviour
    {
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

        [Header("Events")]
        [SerializeField] UnityEvent<GameObject> missileFired;
        [SerializeField] UnityEvent<bool> attackEnabledChanged;

        float nextFireTime;
        bool wasTriggerPressed;

        public bool AttackEnabled => attackEnabled;
        public bool CanAttack => attackEnabled && missilePrefab != null && Time.time >= nextFireTime;
        public GameObject LastFiredMissile { get; private set; }

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
        }

        void Update()
        {
            var action = fireAction != null ? fireAction.action : null;
            if (action == null)
                return;

            var isTriggerPressed = action.ReadValue<float>() >= triggerThreshold;
            if (isTriggerPressed && !wasTriggerPressed)
                TryFire();

            wasTriggerPressed = isTriggerPressed;
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
    }
}
