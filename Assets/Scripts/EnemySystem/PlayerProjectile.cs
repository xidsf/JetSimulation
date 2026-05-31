using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class PlayerProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 120f;
        [SerializeField] private float damage = 1f;
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private float hitRadius = 0.08f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Transform homingTarget;
        [SerializeField] private float homingTurnSpeed = 180f;
        [SerializeField] private bool alignForwardToDirection;
        [SerializeField] private Vector3 visualRotationOffset;

        private Vector3 direction = Vector3.forward;
        private float age;
        private Collider ownCollider;

        private void Awake()
        {
            ownCollider = GetComponent<Collider>();
            EnsurePhysicsSetup();
        }

        private void Update()
        {
            UpdateHomingDirection();

            var previousPosition = transform.position;
            var distance = speed * Time.deltaTime;
            var nextPosition = previousPosition + direction * distance;

            if (Physics.SphereCast(previousPosition, hitRadius, direction, out var hit, distance, hitMask, QueryTriggerInteraction.Collide))
            {
                if (ownCollider == null || hit.collider != ownCollider)
                {
                    if (TryHit(hit.collider.gameObject, hit.point))
                    {
                        return;
                    }
                }
            }

            transform.position = nextPosition;
            UpdateVisualRotation();
            age += Time.deltaTime;

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ownCollider != null && other == ownCollider)
            {
                return;
            }

            TryHit(other.gameObject, transform.position);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            TryHit(collision.gameObject, hitPoint);
        }

        public void Initialize(Vector3 fireDirection, float projectileSpeed, float projectileDamage, float projectileLifetime, float projectileHitRadius)
        {
            direction = fireDirection.sqrMagnitude > 0.001f ? fireDirection.normalized : transform.forward;
            speed = Mathf.Max(0f, projectileSpeed);
            damage = Mathf.Max(0f, projectileDamage);
            lifetime = Mathf.Max(0.1f, projectileLifetime);
            hitRadius = Mathf.Max(0.01f, projectileHitRadius);
            age = 0f;
        }

        public void SetHomingTarget(Transform target, float turnSpeedDegrees, bool shouldAlignForwardToDirection, Vector3 rotationOffset)
        {
            homingTarget = target;
            homingTurnSpeed = Mathf.Max(0f, turnSpeedDegrees);
            alignForwardToDirection = shouldAlignForwardToDirection;
            visualRotationOffset = rotationOffset;
            UpdateVisualRotation();
        }

        private void UpdateHomingDirection()
        {
            if (homingTarget == null)
            {
                return;
            }

            var targetDirection = homingTarget.position - transform.position;
            if (targetDirection.sqrMagnitude < 0.001f)
            {
                return;
            }

            var maxRadians = homingTurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
            direction = Vector3.RotateTowards(direction, targetDirection.normalized, maxRadians, 0f).normalized;
        }

        private void UpdateVisualRotation()
        {
            if (!alignForwardToDirection || direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(visualRotationOffset);
        }

        private bool TryHit(GameObject hitObject, Vector3 hitPoint)
        {
            var damageable = hitObject.GetComponentInParent<Damageable>();
            if (damageable == null)
            {
                return false;
            }

            damageable.TakeDamage(damage, gameObject);

            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
            }

            Destroy(gameObject);
            return true;
        }

        private void EnsurePhysicsSetup()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = true;

            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }
    }
}
