using JetSimulation.Core;
using UnityEngine;

namespace JetSimulation.EnemySystem
{
    public enum EnemyMovementPattern
    {
        Random,
        Straight,
        Horizontal,
        Vertical
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyHealth health;
        [SerializeField] private EnemyStraightMover mover;

        [Header("Movement")]
        [SerializeField] private EnemyMovementPattern movementPattern = EnemyMovementPattern.Random;
        [SerializeField] private float speed = 35f;
        [SerializeField] private float maxTravelDistance = 220f;
        [SerializeField] private float horizontalAmplitude = 8f;
        [SerializeField] private float horizontalMoveInterval = 2f;
        [SerializeField] private float horizontalBankAngle = 25f;
        [SerializeField] private float verticalAmplitude = 3f;
        [SerializeField] private float verticalMoveInterval = 2f;
        [SerializeField] private float maxPatternTurnAngle = 18f;
        [SerializeField] private float despawnBehindReferenceDistance = 200f;
        [SerializeField] private bool rotateToMoveDirection = true;
        [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

        public EnemyHealth Health => health;
        public EnemyMovementPattern CurrentMovementPattern => activeMovementPattern;

        private EnemyMovementPattern activeMovementPattern = EnemyMovementPattern.Straight;
        private Vector3 entryStart;
        private Vector3 entryControl;
        private Vector3 entryEnd;
        private Vector3 moveDirection = Vector3.back;
        private Vector3 sideDirection = Vector3.right;
        private Vector3 movementBasePosition;
        private Vector3 previousPosition;
        private float entryDuration = 1.5f;
        private float entryElapsed;
        private float movementElapsed;
        private float maxTravelDistanceSqr;
        private Transform despawnReference;
        private bool isInitialized;
        private bool isEntering;
        private bool gameOverSubscribed;

        private void Reset()
        {
            CacheComponents();
        }

        private void Awake()
        {
            CacheComponents();
            DisableLegacyMover();
        }

        private void Start()
        {
            TrySubscribeGameOver();

            if (IsGameOver())
            {
                StopMovement();
                return;
            }

            if (!isInitialized)
            {
                Initialize(transform.position, transform.position, transform.forward, 0f, 0f);
            }
        }

        private void Update()
        {
            if (IsGameOver())
            {
                StopMovement();
                return;
            }

            if (!isInitialized)
            {
                return;
            }

            if (isEntering)
            {
                UpdateEntry();
                return;
            }

            UpdateMovement();
        }

        private void OnEnable()
        {
            TrySubscribeGameOver();
        }

        private void OnDisable()
        {
            UnsubscribeGameOver();
        }

        public void Initialize(Vector3 moveDirection, float moveSpeed, float travelDistance)
        {
            speed = Mathf.Max(0f, moveSpeed);
            maxTravelDistance = travelDistance;
            Initialize(transform.position, transform.position, moveDirection, 0f, 0f);
        }

        public void Initialize(Vector3 moveDirection, float moveSpeed, float travelDistance, bool lockHeight, float flightHeight)
        {
            var start = transform.position;
            var end = lockHeight ? new Vector3(start.x, flightHeight, start.z) : start;
            speed = Mathf.Max(0f, moveSpeed);
            maxTravelDistance = travelDistance;
            Initialize(start, end, moveDirection, 0f, 0f);
        }

        public void Initialize(
            Vector3 moveDirection,
            float moveSpeed,
            float travelDistance,
            bool lockHeight,
            float flightHeight,
            bool descendBeforeForward,
            float descentSpeed)
        {
            var start = transform.position;
            var end = lockHeight ? new Vector3(start.x, flightHeight, start.z) : start;
            var duration = descendBeforeForward && descentSpeed > 0f ? Mathf.Abs(start.y - end.y) / descentSpeed : 0f;

            speed = Mathf.Max(0f, moveSpeed);
            maxTravelDistance = travelDistance;
            Initialize(start, end, moveDirection, duration, 0f);
        }

        public void Initialize(
            Vector3 spawnPosition,
            Vector3 entryTargetPosition,
            Vector3 forwardDirection,
            float parabolicEntryDuration,
            float entryArcHeight)
        {
            if (IsGameOver())
            {
                StopMovement();
                return;
            }

            CacheComponents();
            DisableLegacyMover();

            if (health != null)
            {
                health.ResetHealth();
            }

            entryStart = spawnPosition;
            entryEnd = entryTargetPosition;
            entryControl = (entryStart + entryEnd) * 0.5f + Vector3.up * Mathf.Max(0f, entryArcHeight);
            entryDuration = Mathf.Max(0f, parabolicEntryDuration);
            entryElapsed = 0f;
            movementElapsed = 0f;
            maxTravelDistanceSqr = maxTravelDistance * maxTravelDistance;

            moveDirection = NormalizeDirection(forwardDirection, transform.forward);
            sideDirection = Vector3.Cross(Vector3.up, moveDirection);
            if (sideDirection.sqrMagnitude < 0.001f)
            {
                sideDirection = transform.right.sqrMagnitude > 0.001f ? transform.right.normalized : Vector3.right;
            }
            else
            {
                sideDirection.Normalize();
            }

            activeMovementPattern = ResolveMovementPattern();
            transform.position = entryStart;
            previousPosition = transform.position;
            isInitialized = true;
            isEntering = entryDuration > 0f && (entryEnd - entryStart).sqrMagnitude > 0.001f;

            if (!isEntering)
            {
                BeginMovement(entryEnd);
            }
            else
            {
                RotateAlong(moveDirection);
            }
        }

        public void SetDespawnReference(Transform reference, float behindDistance)
        {
            despawnReference = reference;
            despawnBehindReferenceDistance = Mathf.Max(0f, behindDistance);
        }

        public void StopMovement()
        {
            isEntering = false;
            enabled = false;
        }

        private void UpdateEntry()
        {
            entryElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(entryElapsed / entryDuration);
            var nextPosition = EvaluateQuadraticBezier(entryStart, entryControl, entryEnd, SmoothStep(t));

            transform.position = nextPosition;
            RotateAlong(moveDirection);

            if (t >= 1f)
            {
                BeginMovement(entryEnd);
            }
        }

        private void BeginMovement(Vector3 startPosition)
        {
            transform.position = startPosition;
            movementBasePosition = startPosition;
            previousPosition = startPosition;
            movementElapsed = 0f;
            isEntering = false;
            RotateAlong(moveDirection);
        }

        private void UpdateMovement()
        {
            movementElapsed += Time.deltaTime;
            movementBasePosition += moveDirection * speed * Time.deltaTime;

            var offset = Vector3.zero;
            var bankAngle = 0f;
            switch (activeMovementPattern)
            {
                case EnemyMovementPattern.Horizontal:
                    var horizontalPhase = GetOscillationPhase(movementElapsed, horizontalMoveInterval);
                    offset = sideDirection * Mathf.Sin(horizontalPhase) * horizontalAmplitude;
                    bankAngle = -Mathf.Cos(horizontalPhase) * horizontalBankAngle;
                    break;
                case EnemyMovementPattern.Vertical:
                    offset = Vector3.up * Mathf.Sin(GetOscillationPhase(movementElapsed, verticalMoveInterval)) * verticalAmplitude;
                    break;
            }

            var nextPosition = movementBasePosition + offset;
            var visualDirection = nextPosition - previousPosition;
            transform.position = nextPosition;

            if (visualDirection.sqrMagnitude > 0.001f)
            {
                RotateAlong(LimitDirectionAngle(visualDirection.normalized, moveDirection, maxPatternTurnAngle), bankAngle);
            }

            previousPosition = nextPosition;

            if (ShouldDespawnBehindReference())
            {
                Destroy(gameObject);
                return;
            }

            if (despawnReference == null && maxTravelDistance > 0f && (movementBasePosition - entryEnd).sqrMagnitude >= maxTravelDistanceSqr)
            {
                Destroy(gameObject);
            }
        }

        private EnemyMovementPattern ResolveMovementPattern()
        {
            if (movementPattern != EnemyMovementPattern.Random)
            {
                return movementPattern;
            }

            return (EnemyMovementPattern)Random.Range((int)EnemyMovementPattern.Straight, (int)EnemyMovementPattern.Vertical + 1);
        }

        private void RotateAlong(Vector3 direction)
        {
            RotateAlong(direction, 0f);
        }

        private void RotateAlong(Vector3 direction, float bankAngle)
        {
            if (!rotateToMoveDirection || direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction, Vector3.up)
                * Quaternion.AngleAxis(bankAngle, Vector3.forward)
                * Quaternion.Euler(modelRotationOffset);
        }

        private void CacheComponents()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (mover == null)
            {
                mover = GetComponent<EnemyStraightMover>();
            }
        }

        private void DisableLegacyMover()
        {
            if (mover != null)
            {
                mover.enabled = false;
            }
        }

        private static Vector3 NormalizeDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.back;
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float GetOscillationPhase(float elapsed, float interval)
        {
            return elapsed / Mathf.Max(0.1f, interval) * Mathf.PI * 2f;
        }

        private bool ShouldDespawnBehindReference()
        {
            if (despawnReference == null)
            {
                return false;
            }

            return transform.position.z <= despawnReference.position.z - despawnBehindReferenceDistance;
        }

        private void TrySubscribeGameOver()
        {
            if (gameOverSubscribed || GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.OnGameOver += StopMovement;
            gameOverSubscribed = true;
        }

        private void UnsubscribeGameOver()
        {
            if (!gameOverSubscribed || GameManager.Instance == null)
            {
                gameOverSubscribed = false;
                return;
            }

            GameManager.Instance.OnGameOver -= StopMovement;
            gameOverSubscribed = false;
        }

        private static bool IsGameOver()
        {
            return GameManager.Instance != null && GameManager.Instance.IsGameOver;
        }

        private static Vector3 LimitDirectionAngle(Vector3 direction, Vector3 forward, float maxAngle)
        {
            if (maxAngle <= 0f || forward.sqrMagnitude < 0.001f)
            {
                return direction;
            }

            return Vector3.RotateTowards(forward.normalized, direction.normalized, maxAngle * Mathf.Deg2Rad, 0f);
        }
    }
}
