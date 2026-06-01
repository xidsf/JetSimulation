using UnityEngine;

namespace JetSimulation.EnemySystem
{
    public enum ScreenSpawnSide
    {
        Random,
        Top,
        Bottom,
        Left,
        Right
    }

    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private EnemyController[] enemyPrefabs;
        [SerializeField] private Transform enemyParent;

        [Header("Activation")]
        [SerializeField] private float activationDistance = 120f;
        [SerializeField] private bool spawnOnlyOnce = false;
        [SerializeField] private bool startActive = true;

        [Header("Spawn")]
        [SerializeField] private ScreenSpawnSide spawnSide = ScreenSpawnSide.Random;
        [SerializeField] private float spawnDistanceFromCamera = 85f;
        [SerializeField] private float viewportOverscan = 0.08f;
        [SerializeField] private float spawnHeight = 50f;
        [SerializeField] private bool useFixedFlightHeight = true;
        [SerializeField] private float flightHeight = 10f;
        [SerializeField] private float heightJitter = 0f;
        [SerializeField] private float spawnInterval = 2.5f;
        [SerializeField] private int enemiesPerWave = 1;
        [SerializeField] private float waveSpread = 7f;

        [Header("Movement")]
        [SerializeField] private float enemySpeed = 35f;
        [SerializeField] private float maxTravelDistance = 220f;
        [SerializeField] private bool moveHorizontallyOnly = true;
        [SerializeField] private bool descendBeforeForward = true;
        [SerializeField] private float descentSpeed = 25f;

        private float nextSpawnTime;
        private bool hasSpawned;
        private Vector3 lastPlayerPosition;
        private Vector3 fallbackDirection = Vector3.forward;

        private void Awake()
        {
            ResolveReferences();

            if (player != null)
            {
                lastPlayerPosition = player.position;
                fallbackDirection = player.forward.sqrMagnitude > 0.001f ? player.forward.normalized : Vector3.forward;
            }
        }

        private void Update()
        {
            if (!startActive)
            {
                TrackPlayerDirection();
                return;
            }

            if (player == null || playerCamera == null)
            {
                ResolveReferences();
            }

            TrackPlayerDirection();

            if (player == null || playerCamera == null || enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                return;
            }

            if (spawnOnlyOnce && hasSpawned)
            {
                return;
            }

            if (Vector3.Distance(player.position, transform.position) > activationDistance)
            {
                return;
            }

            if (Time.time < nextSpawnTime)
            {
                return;
            }

            SpawnWave();
            hasSpawned = true;
            nextSpawnTime = Time.time + spawnInterval;
        }

        public void SetActive(bool active)
        {
            startActive = active;
        }

        public void SpawnWave()
        {
            var count = Mathf.Max(1, enemiesPerWave);
            for (var i = 0; i < count; i++)
            {
                SpawnEnemy(i, count);
            }
        }

        private void SpawnEnemy(int index, int count)
        {
            var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            if (prefab == null)
            {
                return;
            }

            var side = ResolveSpawnSide();
            var spawnPosition = GetViewportSpawnPosition(side);
            spawnPosition += GetWaveOffset(index, count);
            spawnPosition = ApplySpawnHeight(spawnPosition);

            var moveDirection = GetEnemyMoveDirection();
            var rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            var enemy = Instantiate(prefab, spawnPosition, rotation, enemyParent);
            enemy.Initialize(
                moveDirection,
                enemySpeed,
                maxTravelDistance,
                useFixedFlightHeight,
                flightHeight,
                descendBeforeForward,
                descentSpeed);
        }

        private ScreenSpawnSide ResolveSpawnSide()
        {
            if (spawnSide != ScreenSpawnSide.Random)
            {
                return spawnSide;
            }

            return (ScreenSpawnSide)Random.Range((int)ScreenSpawnSide.Top, (int)ScreenSpawnSide.Right + 1);
        }

        private Vector3 GetViewportSpawnPosition(ScreenSpawnSide side)
        {
            var x = Random.Range(0.2f, 0.8f);
            var y = Random.Range(0.2f, 0.8f);

            switch (side)
            {
                case ScreenSpawnSide.Top:
                    y = 1f + viewportOverscan;
                    break;
                case ScreenSpawnSide.Bottom:
                    y = -viewportOverscan;
                    break;
                case ScreenSpawnSide.Left:
                    x = -viewportOverscan;
                    break;
                case ScreenSpawnSide.Right:
                    x = 1f + viewportOverscan;
                    break;
            }

            return playerCamera.ViewportToWorldPoint(new Vector3(x, y, spawnDistanceFromCamera));
        }

        private Vector3 GetWaveOffset(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.zero;
            }

            var centeredIndex = index - (count - 1) * 0.5f;
            return playerCamera.transform.right * centeredIndex * waveSpread;
        }

        private Vector3 ApplySpawnHeight(Vector3 position)
        {
            if (!useFixedFlightHeight)
            {
                position.y += Random.Range(-heightJitter, heightJitter);
                return position;
            }

            position.y = spawnHeight + Random.Range(-heightJitter, heightJitter);
            return position;
        }

        private void TrackPlayerDirection()
        {
            if (player == null)
            {
                return;
            }

            var delta = player.position - lastPlayerPosition;
            if (delta.sqrMagnitude > 0.01f)
            {
                fallbackDirection = delta.normalized;
            }
            else if (player.forward.sqrMagnitude > 0.001f)
            {
                fallbackDirection = player.forward.normalized;
            }

            lastPlayerPosition = player.position;
        }

        private Vector3 GetPlayerMoveDirection()
        {
            if (fallbackDirection.sqrMagnitude < 0.001f)
            {
                return Vector3.forward;
            }

            return fallbackDirection.normalized;
        }

        private Vector3 GetEnemyMoveDirection()
        {
            var direction = GetPlayerMoveDirection() * -1f;

            if (moveHorizontallyOnly)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = playerCamera != null ? playerCamera.transform.forward * -1f : Vector3.back;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.back;
            }

            return direction.normalized;
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (player == null && playerCamera != null)
            {
                player = playerCamera.transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, activationDistance);
        }
    }
}
