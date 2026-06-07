using JetSimulation.Core;
using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class EnemyStraightMover : MonoBehaviour
    {
        [SerializeField] private float speed = 35f;
        [SerializeField] private float maxTravelDistance = 220f;
        [SerializeField] private bool rotateToMoveDirection = true;
        [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

        private Vector3 moveDirection = Vector3.back;
        private Vector3 spawnPosition;
        private bool initialized;
        private bool lockHeight;
        private float flightHeight;
        private bool descendBeforeForward;
        private float descentSpeed;
        private bool isDescending;
        private float maxTravelDistanceSqr;
        private bool gameOverSubscribed;

        private void Start()
        {
            TrySubscribeGameOver();

            if (IsGameOver())
            {
                StopMovement();
                return;
            }

            if (!initialized)
            {
                Initialize(transform.forward, speed, maxTravelDistance);
            }
        }

        private void OnEnable()
        {
            TrySubscribeGameOver();
        }

        private void OnDisable()
        {
            UnsubscribeGameOver();
        }

        private void Update()
        {
            if (IsGameOver())
            {
                StopMovement();
                return;
            }

            if (isDescending)
            {
                var nextHeight = Mathf.MoveTowards(transform.position.y, flightHeight, descentSpeed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, nextHeight, transform.position.z);

                if (Mathf.Approximately(nextHeight, flightHeight))
                {
                    isDescending = false;
                    spawnPosition = transform.position;
                }

                return;
            }

            var nextPosition = transform.position + moveDirection * speed * Time.deltaTime;
            if (lockHeight)
            {
                nextPosition.y = flightHeight;
            }

            transform.position = nextPosition;

            if (maxTravelDistance > 0f && (transform.position - spawnPosition).sqrMagnitude >= maxTravelDistanceSqr)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(Vector3 direction, float moveSpeed, float travelDistance)
        {
            Initialize(direction, moveSpeed, travelDistance, false, 0f);
        }

        public void Initialize(Vector3 direction, float moveSpeed, float travelDistance, bool shouldLockHeight, float lockedHeight)
        {
            Initialize(direction, moveSpeed, travelDistance, shouldLockHeight, lockedHeight, false, 0f);
        }

        public void Initialize(
            Vector3 direction,
            float moveSpeed,
            float travelDistance,
            bool shouldLockHeight,
            float lockedHeight,
            bool shouldDescendBeforeForward,
            float descendSpeed)
        {
            if (IsGameOver())
            {
                StopMovement();
                return;
            }

            moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.back;
            speed = Mathf.Max(0f, moveSpeed);
            maxTravelDistance = travelDistance;
            maxTravelDistanceSqr = travelDistance * travelDistance;
            lockHeight = shouldLockHeight;
            flightHeight = lockedHeight;
            descendBeforeForward = shouldDescendBeforeForward;
            descentSpeed = Mathf.Max(0.01f, descendSpeed);

            isDescending = lockHeight && descendBeforeForward && transform.position.y > flightHeight;
            if (lockHeight && !isDescending)
            {
                transform.position = new Vector3(transform.position.x, flightHeight, transform.position.z);
            }

            spawnPosition = transform.position;
            initialized = true;

            if (rotateToMoveDirection)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up) * Quaternion.Euler(modelRotationOffset);
            }
        }

        public void StopMovement()
        {
            isDescending = false;
            enabled = false;
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
    }
}
