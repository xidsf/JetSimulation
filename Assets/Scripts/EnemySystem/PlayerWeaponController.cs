using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private GameObject missilePrefab;

        [Header("Input")]
        [SerializeField] private KeyCode missileKey = KeyCode.Q;

        [Header("Bullet")]
        [SerializeField] private float bulletDamage = 1f;
        [SerializeField] private float bulletSpeed = 160f;
        [SerializeField] private float bulletLifetime = 3f;
        [SerializeField] private float bulletCooldown = 0.08f;
        [SerializeField] private float bulletHitRadius = 0.06f;
        [SerializeField] private Vector3 bulletScale = Vector3.one;

        [Header("Missile")]
        [SerializeField] private float missileDamage = 10f;
        [SerializeField] private float missileSpeed = 90f;
        [SerializeField] private float missileLifetime = 6f;
        [SerializeField] private float missileCooldown = 0.6f;
        [SerializeField] private float missileHitRadius = 0.25f;
        [SerializeField] private Vector3 missileScale = Vector3.one;
        [SerializeField] private Vector3 missileRotationOffset;
        [SerializeField] private float missileHomingTurnSpeed = 180f;

        [Header("Spawn")]
        [SerializeField] private float muzzleForwardOffset = 2f;
        [SerializeField] private float muzzleUpOffset = 0.3f;

        private float nextBulletTime;
        private float nextMissileTime;

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        private void Update()
        {
            if (IsFirePressed() && Time.time >= nextBulletTime)
            {
                FireBullet();
                nextBulletTime = Time.time + bulletCooldown;
            }

            if (IsMissilePressed() && Time.time >= nextMissileTime)
            {
                FireMissile();
                nextMissileTime = Time.time + missileCooldown;
            }
        }

        private void FireBullet()
        {
            var direction = GetFireDirection();
            var bullet = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bullet.name = "Player Bullet";
            bullet.transform.position = GetMuzzlePosition(direction);
            bullet.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            bullet.transform.localScale = bulletScale;

            var renderer = bullet.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.yellow;
            }

            var projectile = bullet.AddComponent<PlayerProjectile>();
            projectile.Initialize(direction, bulletSpeed, bulletDamage, bulletLifetime, bulletHitRadius);
        }

        private void FireMissile()
        {
            if (missilePrefab == null)
            {
                return;
            }

            var direction = GetFireDirection();
            var rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(missileRotationOffset);
            var missile = Instantiate(missilePrefab, GetMuzzlePosition(direction), rotation);
            missile.name = "Player Missile";
            missile.transform.localScale = Vector3.Scale(missile.transform.localScale, missileScale);

            EnsureCollider(missile);
            var projectile = missile.GetComponent<PlayerProjectile>();
            if (projectile == null)
            {
                projectile = missile.AddComponent<PlayerProjectile>();
            }

            projectile.Initialize(direction, missileSpeed, missileDamage, missileLifetime, missileHitRadius);
            projectile.SetHomingTarget(FindNearestEnemyToCamera(), missileHomingTurnSpeed, true, missileRotationOffset);
        }

        private Vector3 GetFireDirection()
        {
            ResolveAimCamera();

            if (aimCamera != null)
            {
                return aimCamera.transform.forward.normalized;
            }

            return firePoint != null ? firePoint.forward.normalized : transform.forward.normalized;
        }

        private Vector3 GetMuzzlePosition(Vector3 direction)
        {
            var origin = firePoint != null ? firePoint.position : transform.position;
            return origin + direction * muzzleForwardOffset + Vector3.up * muzzleUpOffset;
        }

        private void ResolveAimCamera()
        {
            if (aimCamera != null && aimCamera.isActiveAndEnabled)
            {
                return;
            }

            aimCamera = Camera.main;
        }

        private Transform FindNearestEnemyToCamera()
        {
            ResolveAimCamera();

            var origin = aimCamera != null ? aimCamera.transform.position : transform.position;
            var enemies = FindObjectsOfType<EnemyHealth>();
            EnemyHealth nearestEnemy = null;
            var nearestDistanceSqr = float.PositiveInfinity;

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                var distanceSqr = (enemy.transform.position - origin).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestEnemy = enemy;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestEnemy != null ? nearestEnemy.transform : null;
        }

        private static void EnsureCollider(GameObject projectile)
        {
            if (projectile.GetComponent<Collider>() != null)
            {
                return;
            }

            var collider = projectile.AddComponent<CapsuleCollider>();
            collider.radius = 0.2f;
            collider.height = 1f;
            collider.direction = 2;
            collider.isTrigger = true;
        }

        private bool IsFirePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private bool IsMissilePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.qKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(missileKey);
#else
            return false;
#endif
        }
    }
}
