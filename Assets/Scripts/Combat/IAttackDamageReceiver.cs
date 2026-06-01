using UnityEngine;

namespace JetSimulation.Combat
{
    public interface IAttackDamageReceiver
    {
        void TakeDamage(float amount, GameObject source);
    }
}
