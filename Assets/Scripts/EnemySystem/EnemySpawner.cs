using JetSimulation.Core;
using UnityEngine;

namespace JetSimulation.EnemySystem
{
    public enum ScreenSpawnSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public enum EnemyPrefabGroup
    {
        Default,
        A10,
        Sr71
    }

    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private EnemyController[] enemyPrefabs;
        [SerializeField] private EnemyController[] a10EnemyPrefabs;
        [SerializeField] private EnemyController[] sr71EnemyPrefabs;
        [SerializeField] private Transform enemyParent;

        [Header("Boss")]
        [SerializeField] private EnemyBossController bossPrefab;
        [SerializeField] private bool enableBoss = true;
        [SerializeField] private float bossSpawnDelay = 10f;

        [Header("Score Unlocks")]
        [SerializeField] private int a10UnlockScore = 1000;
        [SerializeField] private int sr71UnlockScore = 3500;

        [Header("Activation")]
        [SerializeField] private float activationDistance = 650f;
        [SerializeField] private bool spawnOnlyOnce = false;
        [SerializeField] private bool startActive = true;

        [Header("Follow")]
        [SerializeField] private bool followPlayerPosition = true;
        [SerializeField] private float followDistanceFromPlayer = 600f;
        [SerializeField] private Vector3 followOffset = Vector3.zero;

        [Header("Spawn")]
        [SerializeField] private float spawnBoxDepth = 80f;
        [SerializeField] private float spawnBoxWidth = 500f;
        [SerializeField] private float parabolicEntryForwardDistance = 550f;
        [SerializeField] private float sideEntryOffset = 14f;
        [SerializeField] private float verticalScreenOffset = 18f;
        [SerializeField] private float spawnHeight = 50f;
        [SerializeField] private float entryHeightRangeFromPlayer = 100f;
        [SerializeField] private float heightJitter = 0f;
        [SerializeField] private float entryArcHeight = 25f;
        [SerializeField] private float entryDuration = 1.5f;
        [SerializeField] private float despawnBehindPlayerDistance = 200f;
        [SerializeField] private float spawnInterval = 2.5f;
        [SerializeField] private int enemiesPerWave = 1;
        [SerializeField] private float waveSpread = 7f;

        private float nextSpawnTime;
        private float bossTimerStartTime;
        private bool hasSpawned;
        private bool bossSpawnRequested;
        private bool bossSpawned;
        private bool missingBossPrefabLogged;
        private Quaternion fixedRotation;

        private void Awake()
        {
            fixedRotation = transform.rotation;
            bossTimerStartTime = Time.time;
            ResolveReferences();
        }

        private void Update()
        {
            if (player == null || playerCamera == null)
            {
                ResolveReferences();
            }

            FollowPlayerWithoutRotation();

            if (!startActive)
            {
                return;
            }

            if (TryUpdateBossPhase())
            {
                return;
            }

            if (player == null || playerCamera == null || !HasAnySpawnablePrefab())
            {
                return;
            }

            if (spawnOnlyOnce && hasSpawned)
            {
                return;
            }

            if (!followPlayerPosition && Vector3.Distance(player.position, transform.position) > activationDistance)
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

        private bool TryUpdateBossPhase()
        {
            if (!enableBoss)
            {
                return false;
            }

            if (bossSpawned)
            {
                return true;
            }

            if (!bossSpawnRequested && Time.time - bossTimerStartTime >= bossSpawnDelay)
            {
                bossSpawnRequested = true;
                Debug.Log("[EnemySystem] Boss time reached. Normal enemy spawning stopped.");
            }

            if (!bossSpawnRequested)
            {
                return false;
            }

            if (!HasLivingNormalEnemies())
            {
                SpawnBoss();
            }

            return true;
        }

        private void SpawnBoss()
        {
            if (bossSpawned)
            {
                return;
            }

            if (player == null)
            {
                ResolveReferences();
            }

            if (bossPrefab == null)
            {
                if (!missingBossPrefabLogged)
                {
                    Debug.LogWarning("[EnemySystem] Boss spawn requested, but Boss Prefab is not assigned on EnemySpawner.");
                    missingBossPrefabLogged = true;
                }

                return;
            }

            var moveDirection = Vector3.back;
            var boss = Instantiate(bossPrefab, transform.position, Quaternion.LookRotation(moveDirection, Vector3.up), enemyParent);
            boss.Initialize(player, transform.position, moveDirection);
            bossSpawned = true;
            Debug.Log("[EnemySystem] Boss battle started.");
        }

        private static bool HasLivingNormalEnemies()
        {
            for (var i = 0; i < EnemyHealth.ActiveEnemies.Count; i++)
            {
                var enemy = EnemyHealth.ActiveEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (enemy.GetComponent<EnemyBossController>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void LateUpdate()
        {
            if (player == null || playerCamera == null)
            {
                ResolveReferences();
            }

            FollowPlayerWithoutRotation();
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
            var prefab = ChooseEnemyPrefab();
            if (prefab == null)
            {
                return;
            }

            var side = ResolveSpawnSide();
            GetEntryPath(side, out var spawnPosition, out var entryTargetPosition);

            spawnPosition += GetWaveOffset(index, count);
            entryTargetPosition += GetWaveOffset(index, count);

            var moveDirection = Vector3.back;
            var rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            var enemy = Instantiate(prefab, spawnPosition, rotation, enemyParent);
            enemy.Initialize(spawnPosition, entryTargetPosition, moveDirection, entryDuration, entryArcHeight);
            enemy.SetDespawnReference(player, despawnBehindPlayerDistance);
        }

        private EnemyController ChooseEnemyPrefab()
        {
            var currentScore = GetCurrentScore();
            var eligibleCount = CountEligiblePrefabs(currentScore);

            if (eligibleCount <= 0)
            {
                return null;
            }

            var pickIndex = Random.Range(0, eligibleCount);
            var selectedPrefab = PickPrefabByGroup(enemyPrefabs, EnemyPrefabGroup.Default, ref pickIndex);
            if (selectedPrefab != null)
            {
                return selectedPrefab;
            }

            if (currentScore >= a10UnlockScore)
            {
                selectedPrefab = PickPrefabByGroup(enemyPrefabs, EnemyPrefabGroup.A10, ref pickIndex);
                if (selectedPrefab != null)
                {
                    return selectedPrefab;
                }

                selectedPrefab = PickValidPrefab(a10EnemyPrefabs, ref pickIndex);
                if (selectedPrefab != null)
                {
                    return selectedPrefab;
                }
            }

            if (currentScore >= sr71UnlockScore)
            {
                selectedPrefab = PickPrefabByGroup(enemyPrefabs, EnemyPrefabGroup.Sr71, ref pickIndex);
                if (selectedPrefab != null)
                {
                    return selectedPrefab;
                }

                return PickValidPrefab(sr71EnemyPrefabs, ref pickIndex);
            }

            return null;
        }

        private ScreenSpawnSide ResolveSpawnSide()
        {
            return (ScreenSpawnSide)Random.Range((int)ScreenSpawnSide.Top, (int)ScreenSpawnSide.Right + 1);
        }

        private bool HasAnySpawnablePrefab()
        {
            return CountEligiblePrefabs(GetCurrentScore()) > 0;
        }

        private int CountEligiblePrefabs(int currentScore)
        {
            var count = CountPrefabsByGroup(enemyPrefabs, EnemyPrefabGroup.Default);

            if (currentScore >= a10UnlockScore)
            {
                count += CountPrefabsByGroup(enemyPrefabs, EnemyPrefabGroup.A10);
                count += CountValidPrefabs(a10EnemyPrefabs);
            }

            if (currentScore >= sr71UnlockScore)
            {
                count += CountPrefabsByGroup(enemyPrefabs, EnemyPrefabGroup.Sr71);
                count += CountValidPrefabs(sr71EnemyPrefabs);
            }

            return count;
        }

        private static int GetCurrentScore()
        {
            var gameManagerScore = GameManager.Instance != null ? GameManager.Instance.CurrentScore : 0;
            return Mathf.Max(gameManagerScore, EnemyHealth.LocalDebugScore);
        }

        private static int CountValidPrefabs(EnemyController[] prefabs)
        {
            if (prefabs == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPrefabsByGroup(EnemyController[] prefabs, EnemyPrefabGroup group)
        {
            if (prefabs == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null && GetPrefabGroup(prefabs[i]) == group)
                {
                    count++;
                }
            }

            return count;
        }

        private static EnemyController PickValidPrefab(EnemyController[] prefabs, ref int pickIndex)
        {
            if (prefabs == null)
            {
                return null;
            }

            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    continue;
                }

                if (pickIndex == 0)
                {
                    return prefabs[i];
                }

                pickIndex--;
            }

            return null;
        }

        private static EnemyController PickPrefabByGroup(EnemyController[] prefabs, EnemyPrefabGroup group, ref int pickIndex)
        {
            if (prefabs == null)
            {
                return null;
            }

            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null || GetPrefabGroup(prefabs[i]) != group)
                {
                    continue;
                }

                if (pickIndex == 0)
                {
                    return prefabs[i];
                }

                pickIndex--;
            }

            return null;
        }

        private static EnemyPrefabGroup GetPrefabGroup(EnemyController prefab)
        {
            if (prefab == null)
            {
                return EnemyPrefabGroup.Default;
            }

            var prefabName = prefab.name.ToUpperInvariant();
            if (prefabName.Contains("A10") || prefabName.Contains("A-10") || prefabName.Contains("A_10"))
            {
                return EnemyPrefabGroup.A10;
            }

            if (prefabName.Contains("SR71") || prefabName.Contains("SR-71") || prefabName.Contains("SR_71"))
            {
                return EnemyPrefabGroup.Sr71;
            }

            return EnemyPrefabGroup.Default;
        }

        private void GetEntryPath(ScreenSpawnSide side, out Vector3 spawnPosition, out Vector3 entryTargetPosition)
        {
            var center = transform.position;
            var playerCenter = player != null ? player.position : transform.position - Vector3.forward * followDistanceFromPlayer;
            var spawnHalfWidth = GetSpawnBoxHalfWidth();
            var spawnLateralOffset = Random.Range(-spawnHalfWidth, spawnHalfWidth);
            var entryLateralOffset = Random.Range(-sideEntryOffset, sideEntryOffset);
            var heightOffset = Random.Range(-heightJitter, heightJitter);
            var depthOffset = Random.Range(-spawnBoxDepth * 0.5f, spawnBoxDepth * 0.5f);
            var targetHeight = GetRandomEntryHeight(playerCenter.y, heightOffset);

            var spawnX = center.x + spawnLateralOffset;
            var entryX = center.x + entryLateralOffset;
            var spawnY = playerCenter.y + spawnHeight + heightOffset;
            var entryY = targetHeight;
            var spawnZ = center.z + depthOffset;
            var entryZ = playerCenter.z + parabolicEntryForwardDistance;

            switch (side)
            {
                case ScreenSpawnSide.Top:
                    spawnY = playerCenter.y + spawnHeight + Mathf.Abs(verticalScreenOffset) + heightOffset;
                    entryY = targetHeight;
                    break;
                case ScreenSpawnSide.Bottom:
                    spawnY = playerCenter.y + Mathf.Max(1f, spawnHeight - Mathf.Abs(verticalScreenOffset)) + heightOffset;
                    entryY = targetHeight;
                    break;
                case ScreenSpawnSide.Left:
                    spawnX = center.x - spawnHalfWidth;
                    entryX = center.x - sideEntryOffset;
                    spawnY = playerCenter.y + spawnHeight + heightOffset;
                    break;
                case ScreenSpawnSide.Right:
                    spawnX = center.x + spawnHalfWidth;
                    entryX = center.x + sideEntryOffset;
                    spawnY = playerCenter.y + spawnHeight + heightOffset;
                    break;
            }

            spawnPosition = new Vector3(spawnX, spawnY, spawnZ);
            entryTargetPosition = new Vector3(entryX, entryY, entryZ);
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

        private void FollowPlayerWithoutRotation()
        {
            transform.rotation = fixedRotation;

            if (!followPlayerPosition || player == null)
            {
                return;
            }

            transform.position = player.position + Vector3.forward * followDistanceFromPlayer + followOffset;
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    playerCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            if (player == null && playerCamera != null)
            {
                player = playerCamera.transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.35f);
            DrawSpawnBoxGizmo();
        }

        private void DrawSpawnBoxGizmo()
        {
            var minY = -Mathf.Abs(entryHeightRangeFromPlayer) - Mathf.Abs(heightJitter);
            var maxY = spawnHeight + Mathf.Abs(verticalScreenOffset) + Mathf.Abs(entryHeightRangeFromPlayer) + Mathf.Abs(heightJitter);
            var minZ = Mathf.Min(parabolicEntryForwardDistance - followDistanceFromPlayer, -spawnBoxDepth * 0.5f);
            var maxZ = spawnBoxDepth * 0.5f;
            var size = new Vector3(Mathf.Max(1f, spawnBoxWidth), Mathf.Max(1f, maxY - minY), Mathf.Max(1f, maxZ - minZ));
            var center = transform.position + new Vector3(0f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);

            Gizmos.DrawWireCube(center, size);
        }

        private float GetSpawnBoxHalfWidth()
        {
            return Mathf.Max(0.5f, spawnBoxWidth * 0.5f);
        }

        private float GetRandomEntryHeight(float playerHeight, float heightOffset)
        {
            var range = Mathf.Abs(entryHeightRangeFromPlayer);
            return playerHeight + Random.Range(-range, range) + heightOffset;
        }
    }
}
