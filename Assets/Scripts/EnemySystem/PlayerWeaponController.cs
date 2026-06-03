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

        // 락온 미사일은 MissileAttackController가 훨씬 훌륭하게 처리하므로,
        // 이 스크립트에서는 기총(Bullet) 발사에만 집중하도록 미사일 관련 코드는 주석 처리/삭제해도 무방합니다.
        // [SerializeField] private GameObject missilePrefab;

        [Header("Bullet (Machine Gun)")]
        [SerializeField] private float bulletDamage = 1f;
        [SerializeField] private float bulletSpeed = 160f;
        [SerializeField] private float bulletLifetime = 3f;
        [SerializeField] private float bulletCooldown = 0.08f;
        [SerializeField] private float bulletHitRadius = 0.06f;
        [SerializeField] private Vector3 bulletScale = Vector3.one;

        [Header("Spawn")]
        [SerializeField] private float muzzleForwardOffset = 2f;
        [SerializeField] private float muzzleUpOffset = 0.3f;

        private float nextBulletTime;

        private void Awake()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (firePoint == null) firePoint = transform;
        }

        private void Update()
        {
            // 연사(Machine Gun) 구현: 쿨타임이 찼고 트리거가 당겨져 있다면 계속 발사
            if (IsFirePressed() && Time.time >= nextBulletTime)
            {
                FireBullet();
                nextBulletTime = Time.time + bulletCooldown;
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
                renderer.material.color = Color.yellow; // 예광탄 느낌의 노란색
            }

            var projectile = bullet.AddComponent<PlayerProjectile>();
            projectile.Initialize(direction, bulletSpeed, bulletDamage, bulletLifetime, bulletHitRadius);
        }

        private Vector3 GetFireDirection()
        {
            if (aimCamera != null) return aimCamera.transform.forward.normalized;
            return firePoint != null ? firePoint.forward.normalized : transform.forward.normalized;
        }

        private Vector3 GetMuzzlePosition(Vector3 direction)
        {
            var origin = firePoint != null ? firePoint.position : transform.position;
            return origin + direction * muzzleForwardOffset + Vector3.up * muzzleUpOffset;
        }

        /// <summary>
        /// VR 컨트롤러 트리거 입력과 PC 마우스 입력을 동시에 지원하도록 수정
        /// </summary>
        private bool IsFirePressed()
        {
            // 1. VR 모드: VRInputManager의 오른손 트리거 당김 정도가 0.5 이상일 때 발사
            if (VRInputManager.Instance != null && VRInputManager.Instance.TriggerValue >= 0.5f)
            {
                return true;
            }

            // 2. PC 디버그 모드: 마우스 좌클릭 (연사를 위해 isPressed 유지 확인)
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif
            return false;
        }
    }
}