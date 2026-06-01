using UnityEngine;

namespace JetSimulation.EnemySystem
{
    public interface Damageable
    {
        void TakeDamage(float damage, GameObject source);
    }
}
