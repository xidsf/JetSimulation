using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class ProjectileDamage : MonoBehaviour
    {
        [SerializeField] private float damage = 25f;
        [SerializeField] private bool destroyOnHit = true;

        private void OnTriggerEnter(Collider other)
        {
            TryDealDamage(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDealDamage(collision.gameObject);
        }

        private void TryDealDamage(GameObject hitObject)
        {
            var damageable = hitObject.GetComponentInParent<Damageable>();
            if (damageable == null)
            {
                return;
            }

            damageable.TakeDamage(damage, gameObject);

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
    }
}
