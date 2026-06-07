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
        [SerializeField] private GameObject bossMissilePrefab;

        [Header("Movement")]
        [SerializeField] private float normalEnemySpeed = 35f;
        [SerializeField] private float hoverDistanceFromPlayer = 400f;
        [SerializeField] private float entrySpeedMultiplier = 3f;
        [SerializeField] private float rotationLerpSpeed = 4f;

        [Header("Missile")]
        [SerializeField] private float missileCooldown = 1f;
        [SerializeField] private float missileSpeed = 130f;
        [SerializeField] private float missileDamage = 15f;
        [SerializeField] private float missileLifetime = 8f;
        [SerializeField] private float missileScale = 1.5f;
        [SerializeField] private bool enableMissileAttack = true;
        [SerializeField] private bool enableMissilePrediction = true;
        [SerializeField] private float missilePredictionTime = 1f;

        private Vector3 moveDirection = Vector3.back;
        private Vector3 previousPlayerPosition;
        private Vector3 estimatedPlayerVelocity;
        private float nextMissileTime;
        private bool isEntering;
        private bool isEnded;
        private bool hasPlayerTrackingPosition;
        private bool gameOverSubscribed;

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

            TrySubscribeGameOver();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.destroyed.RemoveListener(HandleBossDestroyed);
            }

            UnsubscribeGameOver();
        }

        private void Update()
        {
            TrySubscribeGameOver();

            if (IsGameOver())
            {
                StopForGameOver();
                return;
            }

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
            UpdatePlayerVelocityEstimate();
            UpdateMissileAttack();
        }

        public void Initialize(Transform targetPlayer)
        {
            Initialize(targetPlayer, transform.position, Vector3.back);
        }

        public void Initialize(Transform targetPlayer, Vector3 spawnPosition, Vector3 forwardDirection)
        {
            if (IsGameOver())
            {
                StopForGameOver();
                return;
            }

            player = targetPlayer;
            ResetPlayerVelocityTracking();
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
            if (!enableMissileAttack || IsGameOver())
            {
                return;
            }

            var origin = firePoint.position;

            if (Time.time < nextMissileTime)
            {
                return;
            }

            if (!IsFinite(origin) || !IsFinite(player.position))
            {
                return;
            }

            var targetPosition = GetPredictedPlayerPosition();
            FireMissile(origin, targetPosition);
            nextMissileTime = Time.time + missileCooldown;
        }

        private void UpdatePlayerVelocityEstimate()
        {
            if (player == null || !IsFinite(player.position))
            {
                hasPlayerTrackingPosition = false;
                estimatedPlayerVelocity = Vector3.zero;
                return;
            }

            if (!hasPlayerTrackingPosition)
            {
                previousPlayerPosition = player.position;
                estimatedPlayerVelocity = Vector3.zero;
                hasPlayerTrackingPosition = true;
                return;
            }

            if (Time.deltaTime > 0.0001f)
            {
                estimatedPlayerVelocity = (player.position - previousPlayerPosition) / Time.deltaTime;
            }

            previousPlayerPosition = player.position;
        }

        private Vector3 GetPredictedPlayerPosition()
        {
            if (!enableMissilePrediction)
            {
                return player.position;
            }

            var predictionTime = Mathf.Max(0f, missilePredictionTime);
            var predictedPosition = player.position + estimatedPlayerVelocity * predictionTime;

            return IsFinite(predictedPosition) ? predictedPosition : player.position;
        }

        private void FireMissile(Vector3 origin, Vector3 targetPosition)
        {
            var direction = targetPosition - origin;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
            }

            direction.Normalize();
            if (!IsFinite(direction))
            {
                return;
            }

            var missilePosition = origin + direction * 3f;
            if (!IsFinite(missilePosition))
            {
                return;
            }

            var missileRotation = Quaternion.LookRotation(direction, GetSafeUp(direction));

            var missile = bossMissilePrefab != null
                ? Instantiate(bossMissilePrefab, missilePosition, missileRotation)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            missile.name = "Boss Straight Missile";
            missile.transform.SetPositionAndRotation(missilePosition, missileRotation);
            missile.transform.localScale = Vector3.one * Mathf.Max(0.01f, missileScale);

            var collider = missile.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            if (!missile.TryGetComponent(out Rigidbody body))
            {
                body = missile.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = true;

            var renderer = missile.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.magenta;
            }

            if (!missile.TryGetComponent(out BossStraightMissile payload))
            {
                payload = missile.AddComponent<BossStraightMissile>();
            }

            payload.Initialize(targetPosition, missileSpeed, missileDamage, missileLifetime);
        }

        private void HandleBossDestroyed()
        {
            if (isEnded)
            {
                return;
            }

            isEnded = true;
            Debug.Log("[EnemySystem] Boss destroyed. Game clear.");

            // GameManager에게 보스가 죽었음을 알림
            if (GameManager.Instance != null)
            {
                GameManager.Instance.HandleBossCleared();
            }
        }

        public void StopForGameOver()
        {
            isEnded = true;
            enableMissileAttack = false;
            enabled = false;
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
                ResetPlayerVelocityTracking();
                return;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                player = camera.transform;
                ResetPlayerVelocityTracking();
            }
        }

        private void ResetPlayerVelocityTracking()
        {
            hasPlayerTrackingPosition = false;
            estimatedPlayerVelocity = Vector3.zero;
        }

        private void TrySubscribeGameOver()
        {
            if (gameOverSubscribed || GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.OnGameOver += StopForGameOver;
            gameOverSubscribed = true;
        }

        private void UnsubscribeGameOver()
        {
            if (!gameOverSubscribed || GameManager.Instance == null)
            {
                gameOverSubscribed = false;
                return;
            }

            GameManager.Instance.OnGameOver -= StopForGameOver;
            gameOverSubscribed = false;
        }

        private static bool IsGameOver()
        {
            return GameManager.Instance != null && GameManager.Instance.IsGameOver;
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

        private static Vector3 GetSafeUp(Vector3 forward)
        {
            return Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
