using JetSimulation.Core;
using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyBossController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyHealth health;
        [SerializeField] private Transform player;
        [SerializeField] private Transform firePoint;

        [Header("Movement")]
        [SerializeField] private float normalEnemySpeed = 35f;
        [SerializeField] private float hoverDistanceFromPlayer = 400f;
        [SerializeField] private float entrySpeedMultiplier = 3f;
        [SerializeField] private float rotationLerpSpeed = 4f;

        [Header("Targeting")]
        [SerializeField] private float laserRange = 900f;
        [SerializeField] private float laserHitRadius = 6f;

        [Header("Missile")]
        [SerializeField] private float missileCooldown = 2f;
        [SerializeField] private float missileSpeed = 130f;
        [SerializeField] private float missileDamage = 15f;
        [SerializeField] private float missileLifetime = 8f;
        [SerializeField] private float missileScale = 1.5f;

        private Vector3 moveDirection = Vector3.back;
        private float nextMissileTime;
        private bool isEntering;
        private bool isEnded;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (firePoint == null)
            {
                firePoint = transform;
            }

        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.destroyed.AddListener(HandleBossDestroyed);
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.destroyed.RemoveListener(HandleBossDestroyed);
            }
        }

        private void Update()
        {
            ResolvePlayer();
            if (isEnded)
            {
                return;
            }

            UpdateMovement();

            if (player == null)
            {
                AimAlongMoveDirection();
                return;
            }

            AimAtPlayer();
            UpdateMissileAttack();
        }

        public void Initialize(Transform targetPlayer)
        {
            Initialize(targetPlayer, transform.position, Vector3.back);
        }

        public void Initialize(Transform targetPlayer, Vector3 spawnPosition, Vector3 forwardDirection)
        {
            player = targetPlayer;
            ResolvePlayer();

            moveDirection = NormalizeDirection(forwardDirection);
            transform.position = spawnPosition;
            isEntering = player != null && Vector3.Distance(transform.position, GetHoverPosition()) > 0.01f;

            if (player != null)
            {
                AimAtPlayer(true);
            }
            else
            {
                AimAlongMoveDirection(true);
            }
        }

        private void UpdateMovement()
        {
            if (player == null)
            {
                return;
            }

            var hoverPosition = GetHoverPosition();
            if (!isEntering)
            {
                transform.position = hoverPosition;
                return;
            }

            var speed = normalEnemySpeed * entrySpeedMultiplier;
            transform.position = Vector3.MoveTowards(transform.position, hoverPosition, speed * Time.deltaTime);

            if ((hoverPosition - transform.position).sqrMagnitude <= 0.01f)
            {
                transform.position = hoverPosition;
                isEntering = false;
            }
        }

        private Vector3 GetHoverPosition()
        {
            return player.position - moveDirection * Mathf.Max(0f, hoverDistanceFromPlayer);
        }

        private void AimAtPlayer(bool snap = false)
        {
            var direction = player.position - transform.position;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = snap
                ? targetRotation
                : Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
        }

        private void AimAlongMoveDirection(bool snap = false)
        {
            var targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = snap
                ? targetRotation
                : Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
        }

        private void UpdateMissileAttack()
        {
            var origin = firePoint.position;
            var direction = (player.position - origin).normalized;

            if (Time.time < nextMissileTime)
            {
                return;
            }

            if (!IsPlayerOnLaserPath(origin, direction))
            {
                return;
            }

            FireMissile(origin, direction);
            nextMissileTime = Time.time + missileCooldown;
        }

        private bool IsPlayerOnLaserPath(Vector3 origin, Vector3 direction)
        {
            var toPlayer = player.position - origin;
            var projectedDistance = Vector3.Dot(toPlayer, direction);
            if (projectedDistance < 0f || projectedDistance > laserRange)
            {
                return false;
            }

            var closestPoint = origin + direction * projectedDistance;
            return (player.position - closestPoint).sqrMagnitude <= laserHitRadius * laserHitRadius;
        }

        private void FireMissile(Vector3 origin, Vector3 direction)
        {
            var missile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            missile.name = "Boss Laser Missile";
            missile.transform.position = origin + direction * 3f;
            missile.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            missile.transform.localScale = Vector3.one * missileScale;

            var collider = missile.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var body = missile.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            var renderer = missile.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.magenta;
            }

            var payload = missile.AddComponent<EnemyBossMissile>();
            payload.Initialize(direction, missileSpeed, missileDamage, missileLifetime);
        }

        private void HandleBossDestroyed()
        {
            if (isEnded)
            {
                return;
            }

            isEnded = true;
            Debug.Log("[EnemySystem] Boss destroyed. Game clear.");
            Time.timeScale = 0f;
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                return;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                player = camera.transform;
            }
        }

        private static Vector3 NormalizeDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return Vector3.back;
            }

            return direction.normalized;
        }
    }
}
