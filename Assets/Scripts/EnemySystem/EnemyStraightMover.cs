using UnityEngine;

namespace JetSimulation.EnemySystem
{
    [DisallowMultipleComponent]
    public sealed class EnemyStraightMover : MonoBehaviour
    {
        [SerializeField] private float speed = 35f;
        [SerializeField] private float maxTravelDistance = 220f;
        [SerializeField] private bool rotateToMoveDirection = true;
        [SerializeField] private Vector3 modelRotationOffset = new Vector3(-90f, 0f, 0f);

        private Vector3 moveDirection = Vector3.back;
        private Vector3 spawnPosition;
        private bool initialized;

        private void Start()
        {
            if (!initialized)
            {
                Initialize(transform.forward, speed, maxTravelDistance);
            }
        }

        private void Update()
        {
            transform.position += moveDirection * speed * Time.deltaTime;

            if (maxTravelDistance > 0f && Vector3.Distance(spawnPosition, transform.position) >= maxTravelDistance)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(Vector3 direction, float moveSpeed, float travelDistance)
        {
            moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.back;
            speed = Mathf.Max(0f, moveSpeed);
            maxTravelDistance = travelDistance;
            spawnPosition = transform.position;
            initialized = true;

            if (rotateToMoveDirection)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up) * Quaternion.Euler(modelRotationOffset);
            }
        }
    }
}
