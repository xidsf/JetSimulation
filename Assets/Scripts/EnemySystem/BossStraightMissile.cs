using System.Reflection;
using JetSimulation.Core;
using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BossStraightMissile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float speed = 130f;
        [SerializeField] private float damage = 15f;
        [SerializeField] private float lifetime = 8f;
        [SerializeField] private float hitRadius = 1.25f;

        [Header("Effects")]
        [SerializeField] private AudioSource launchSound;
        [SerializeField] private AudioSource thrustSound;
        [SerializeField] private GameObject smokePrefab;
        [SerializeField] private Vector3 smokeLocalOffset = new Vector3(0f, 0f, -0.85f);
        [SerializeField] private Vector3 smokeLocalEulerAngles;
        [SerializeField] private GameObject destroyEffectPrefab;

        private Rigidbody projectileRigidbody;
        private ParticleSystem smokeInstance;
        private Vector3 direction = Vector3.forward;
        private float destroyTime;
        private bool isLaunched;
        private bool hasHitPlayer;

        private static readonly FieldInfo CurrentHealthField =
            typeof(PlayerHealth).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo MaxHealthField =
            typeof(PlayerHealth).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            projectileRigidbody = GetComponent<Rigidbody>();
            projectileRigidbody.useGravity = false;
            projectileRigidbody.isKinematic = true;
        }

        public void Initialize(Vector3 targetPosition, float launchSpeed, float missileDamage, float missileLifetime)
        {
            var toTarget = targetPosition - transform.position;
            direction = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
            if (!IsFinite(direction) || direction.sqrMagnitude < 0.001f)
            {
                Destroy(gameObject);
                return;
            }

            speed = Mathf.Max(0f, launchSpeed);
            damage = Mathf.Max(0f, missileDamage);
            lifetime = Mathf.Max(0.1f, missileLifetime);
            destroyTime = Time.time + lifetime;
            isLaunched = true;

            transform.rotation = Quaternion.LookRotation(direction, GetSafeUp(direction));

            if (launchSound != null)
            {
                launchSound.Play();
            }

            if (thrustSound != null)
            {
                thrustSound.Play();
            }

            if (smokePrefab != null)
            {
                var smokeObject = Instantiate(smokePrefab, transform);
                smokeObject.transform.localPosition = smokeLocalOffset;
                smokeObject.transform.localRotation = Quaternion.Euler(smokeLocalEulerAngles);
                smokeObject.transform.localScale = Vector3.one;

                smokeInstance = smokeObject.GetComponent<ParticleSystem>();
                if (smokeInstance != null)
                {
                    smokeInstance.Play();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!isLaunched)
            {
                return;
            }

            var nextPosition = transform.position + direction * speed * Time.fixedDeltaTime;
            if (!IsFinite(nextPosition))
            {
                DestroyMissile(false);
                return;
            }

            if (TryDamagePlayerOnPath(transform.position, nextPosition))
            {
                return;
            }

            projectileRigidbody.MovePosition(nextPosition);

            if (Time.time >= destroyTime)
            {
                DestroyMissile(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamagePlayer(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDamagePlayer(collision.gameObject);
        }

        private bool TryDamagePlayerOnPath(Vector3 currentPosition, Vector3 nextPosition)
        {
            var move = nextPosition - currentPosition;
            var distance = move.magnitude;
            if (distance <= 0.001f)
            {
                return false;
            }

            if (Physics.SphereCast(
                    currentPosition,
                    Mathf.Max(0.01f, hitRadius),
                    move / distance,
                    out var hit,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
            {
                if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }

                return TryDamagePlayer(hit.collider.gameObject);
            }

            return false;
        }

        private bool TryDamagePlayer(GameObject hitObject)
        {
            if (!isLaunched || hasHitPlayer)
            {
                return false;
            }

            var playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return false;
            }

            hasHitPlayer = true;
            var beforeHealth = ReadHealth(playerHealth);
            var beforeMaxHealth = ReadMaxHealth(playerHealth);

            playerHealth.TakeDamage(damage, gameObject);

            var afterHealth = ReadHealth(playerHealth);
            if (beforeHealth.HasValue && afterHealth.HasValue)
            {
                var maxHealthText = beforeMaxHealth.HasValue ? $"/{beforeMaxHealth.Value:0.#}" : string.Empty;
                Debug.Log(
                    $"[EnemySystem] Boss missile damaged player. Damage: {damage:0.#}, Health: {beforeHealth.Value:0.#} -> {afterHealth.Value:0.#}{maxHealthText}");
            }
            else
            {
                Debug.Log($"[EnemySystem] Boss missile damaged player. Damage: {damage:0.#}");
            }

            DestroyMissile(true);
            return true;
        }

        private void DestroyMissile(bool spawnEffect)
        {
            isLaunched = false;

            if (smokeInstance != null)
            {
                smokeInstance.transform.SetParent(null);
                smokeInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(smokeInstance.gameObject, 3f);
            }

            if (spawnEffect && destroyEffectPrefab != null && IsFinite(transform.position))
            {
                var effect = Instantiate(destroyEffectPrefab, transform.position, transform.rotation);
                Destroy(effect, 4f);
            }

            Destroy(gameObject);
        }

        private static Vector3 GetSafeUp(Vector3 forward)
        {
            return Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float? ReadHealth(PlayerHealth playerHealth)
        {
            return CurrentHealthField != null ? (float?)CurrentHealthField.GetValue(playerHealth) : null;
        }

        private static float? ReadMaxHealth(PlayerHealth playerHealth)
        {
            return MaxHealthField != null ? (float?)MaxHealthField.GetValue(playerHealth) : null;
        }
    }
}
