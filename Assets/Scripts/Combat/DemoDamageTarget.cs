using UnityEngine;

namespace JetSimulation.Combat
{
    public sealed class DemoDamageTarget : MonoBehaviour, IAttackDamageReceiver
    {
        [SerializeField] float health = 100f;
        [SerializeField] bool destroyWhenDead = false;

        public float Health => health;

        public void TakeDamage(float amount, GameObject source)
        {
            health -= amount;
            Debug.Log($"{name} took {amount} damage from {source.name}. Remaining health: {health}", this);

            if (destroyWhenDead && health <= 0f)
                Destroy(gameObject);
        }
    }
}
