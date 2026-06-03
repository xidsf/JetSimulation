using HomingMissile;
using JetSimulation.EnemySystem;
using UnityEngine;

namespace JetSimulation.Combat
{
    [DisallowMultipleComponent]
    public sealed class MissilePayload : MonoBehaviour
    {
        [SerializeField] float damage = 35f;
        [SerializeField] float lifeTime = 8f;
        [SerializeField] float armingDelay = 0.1f;

        GameObject owner;
        homing_missile homingMissile;
        float spawnTime;
        bool initialized;

        public void Initialize(float damageAmount, GameObject source, float maxLifeTime, homing_missile missile)
        {
            damage = damageAmount;
            owner = source;
            lifeTime = maxLifeTime;
            homingMissile = missile;
            spawnTime = Time.time;
            initialized = true;
        }

        void OnEnable()
        {
            spawnTime = Time.time;
        }

        void Update()
        {
            if (!initialized)
                return;

            if (Time.time - spawnTime >= lifeTime)
                DestroyProjectile();
        }

        void OnTriggerEnter(Collider other)
        {
            TryHit(other.gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            TryHit(collision.gameObject);
        }

        void TryHit(GameObject hitObject)
        {
            if (!initialized || Time.time - spawnTime < armingDelay)
                return;

            if (IsOwner(hitObject))
                return;

            DealDamage(hitObject);

            DestroyProjectile();
        }

        bool IsOwner(GameObject hitObject)
        {
            if (owner == null || hitObject == null)
                return false;

            return hitObject == owner ||
                   hitObject.transform.IsChildOf(owner.transform) ||
                   owner.transform.IsChildOf(hitObject.transform);
        }

        void DealDamage(GameObject hitObject)
        {
            if (hitObject == null)
                return;

            var source = owner != null ? owner : gameObject;

            var behaviours = hitObject.GetComponentsInParent<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is Damageable damageable)
                {
                    damageable.TakeDamage(damage, source);
                    return;
                }
            }

            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAttackDamageReceiver receiver)
                {
                    receiver.TakeDamage(damage, source);
                    return;
                }
            }
        }

        void DestroyProjectile()
        {
            if (homingMissile != null && homingMissile.isactive)
            {
                if (homingMissile.smoke != null)
                    homingMissile.DestroyMe();
                else
                    Destroy(gameObject);

                return;
            }

            Destroy(gameObject);
        }
    }
}
