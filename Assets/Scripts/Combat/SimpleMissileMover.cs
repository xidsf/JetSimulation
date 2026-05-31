using UnityEngine;

namespace JetSimulation.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleMissileMover : MonoBehaviour
    {
        Rigidbody body;
        float speed;

        public void Launch(Vector3 direction, float launchSpeed)
        {
            if (body == null)
                body = GetComponent<Rigidbody>();

            speed = launchSpeed;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.velocity = direction.normalized * speed;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (speed > 0f)
                body.velocity = transform.forward * speed;
        }
    }
}
