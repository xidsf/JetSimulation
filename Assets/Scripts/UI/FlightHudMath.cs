using UnityEngine;

namespace JetSimulation.UI
{
    internal static class FlightHudMath
    {
        public static float GetPitchDegrees(Transform target)
        {
            if (target == null)
                return 0f;

            Vector3 forward = target.forward;
            Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return Mathf.Atan2(forward.y, horizontalForward.magnitude) * Mathf.Rad2Deg;
        }

        public static float GetRollDegrees(Transform target)
        {
            if (target == null)
                return 0f;

            Vector3 forward = target.forward;
            Vector3 projectedWorldUp = Vector3.ProjectOnPlane(Vector3.up, forward);
            Vector3 projectedAircraftUp = Vector3.ProjectOnPlane(target.up, forward);

            if (projectedWorldUp.sqrMagnitude <= 0.0001f || projectedAircraftUp.sqrMagnitude <= 0.0001f)
                return 0f;

            return Vector3.SignedAngle(projectedWorldUp.normalized, projectedAircraftUp.normalized, forward);
        }

        public static float GetHeadingDegrees(Transform target)
        {
            if (target == null)
                return 0f;

            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
                return 0f;

            return Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
        }
    }
}
