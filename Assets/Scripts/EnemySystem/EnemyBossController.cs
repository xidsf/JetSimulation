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
        [SerializeField] private float entryDistance = 50f;
        [SerializeField] private float entrySpeedMultiplier = 3f;
        [SerializeField] private float cruiseSpeedMultiplier = 0.5f;
        [SerializeField] private float rotationLerpSpeed = 4f;

        [Header("Laser")]
        [SerializeField] private float laserRange = 900f;
        [SerializeField] private float laserWidth = 0.18f;
        [SerializeField] private float laserHitRadius = 6f;
        [SerializeField] private Color laserColor = Color.red;

        [Header("Missile")]
        [SerializeField] private float missileCooldown = 2f;
        [SerializeField] private float missileSpeed = 130f;
        [SerializeField] private float missileDamage = 15f;
        [SerializeField] private float missileLifetime = 8f;
        [SerializeField] private float missileScale = 1.5f;

        private LineRenderer laserLine;
        private Vector3 moveDirection = Vector3.back;
        private Vector3 entryTargetPosition;
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

            CreateLaserLine();
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
                SetLaserVisible(false);
                return;
            }

            UpdateMovement();

            if (player == null)
            {
                SetLaserVisible(false);
                AimAlongMoveDirection();
                return;
            }

            AimAtPlayer();
            UpdateLaser();
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
            entryTargetPosition = spawnPosition + moveDirection * Mathf.Max(0f, entryDistance);
            isEntering = entryDistance > 0f;

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
            var speed = normalEnemySpeed * (isEntering ? entrySpeedMultiplier : cruiseSpeedMultiplier);
            transform.position += moveDirection * speed * Time.deltaTime;

            if (isEntering && Vector3.Dot(entryTargetPosition - transform.position, moveDirection) <= 0f)
            {
                transform.position = entryTargetPosition;
                isEntering = false;
            }
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

        private void UpdateLaser()
        {
            var origin = firePoint.position;
            var direction = (player.position - origin).normalized;
            var end = origin + direction * laserRange;

            SetLaserVisible(true);
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, end);

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
            SetLaserVisible(false);
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

        private void CreateLaserLine()
        {
            laserLine = gameObject.AddComponent<LineRenderer>();
            laserLine.positionCount = 2;
            laserLine.startWidth = laserWidth;
            laserLine.endWidth = laserWidth;
            laserLine.useWorldSpace = true;
            laserLine.material = new Material(Shader.Find("Sprites/Default"));
            laserLine.startColor = laserColor;
            laserLine.endColor = laserColor;
            SetLaserVisible(false);
        }

        private void SetLaserVisible(bool visible)
        {
            if (laserLine != null)
            {
                laserLine.enabled = visible;
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
