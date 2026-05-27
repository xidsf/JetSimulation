using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyStraightMover))]
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        [SerializeField] private EnemyStraightMover mover;

        public EnemyHealth Health => health;
        public EnemyStraightMover Mover => mover;

        private void Reset()
        {
            CacheComponents();
        }

        private void Awake()
        {
            CacheComponents();
        }

        public void Initialize(Vector3 moveDirection, float speed, float maxTravelDistance)
        {
            CacheComponents();
            health.ResetHealth();
            mover.Initialize(moveDirection, speed, maxTravelDistance);
        }

        private void CacheComponents()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (mover == null)
            {
                mover = GetComponent<EnemyStraightMover>();
            }
        }
    }
}
