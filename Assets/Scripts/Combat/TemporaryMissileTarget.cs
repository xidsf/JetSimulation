using UnityEngine;

namespace JetSimulation.Combat
{
    public sealed class TemporaryMissileTarget : MonoBehaviour
    {
        float destroyAt;

        public void Initialize(float lifeTime)
        {
            destroyAt = Time.time + lifeTime;
        }

        void Update()
        {
            if (Time.time >= destroyAt)
                Destroy(gameObject);
        }
    }
}
