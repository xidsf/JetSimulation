using UnityEngine;

namespace JetSimulation.UI
{
    public sealed class FlightHUDRoot : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform aircraftTransform;
        [SerializeField] private bool autoFindAircraft = true;

        [Header("HUD")]
        [SerializeField] private Color hudColor = Color.green;
        [SerializeField, InspectorName("Build On Start")] private bool buildOnAwake = true;

        public Transform AircraftTransform => aircraftTransform;
        public Color HudColor => hudColor;

        private void Awake()
        {
            ResolveAircraftTransform();
        }

        private void OnEnable()
        {
            ResolveAircraftTransform();
        }

        private void Start()
        {
            ResolveAircraftTransform();

            if (buildOnAwake)
                EnsureHudHierarchy();
        }

        private void Reset()
        {
            autoFindAircraft = true;
        }

        public void EnsureHudHierarchy()
        {
            RectTransform flightRoot = EnsureChildRoot(transform, "FlightHUDRoot");
            HudGraphicFactory.StretchToParent(flightRoot);

            RectTransform centerRoot = EnsureChildRoot(flightRoot, "CenterReferenceRoot");
            CenterReferenceHUD centerReference = EnsureComponent<CenterReferenceHUD>(centerRoot);
            centerReference.ApplyDefaults(hudColor);

            RectTransform pitchRoot = EnsureChildRoot(flightRoot, "PitchLadderRoot");
            AttitudePitchLadderHUD pitchLadder = EnsureComponent<AttitudePitchLadderHUD>(pitchRoot);
            pitchLadder.ApplyDefaults(aircraftTransform, hudColor);

            RectTransform bankRoot = EnsureChildRoot(flightRoot, "BankAngleRoot");
            BankAngleHUD bankAngle = EnsureComponent<BankAngleHUD>(bankRoot);
            bankAngle.ApplyDefaults(aircraftTransform, hudColor);

            RectTransform compassRoot = EnsureChildRoot(flightRoot, "HeadingCompassRoot");
            HeadingCompassHUD headingCompass = EnsureComponent<HeadingCompassHUD>(compassRoot);
            headingCompass.ApplyDefaults(aircraftTransform, hudColor);
        }

        private void ResolveAircraftTransform()
        {
            if (aircraftTransform != null || !autoFindAircraft)
                return;

            PlayerJetController jetController = FindObjectOfType<PlayerJetController>();
            if (jetController != null)
                aircraftTransform = jetController.transform;
        }

        private static RectTransform EnsureChildRoot(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
                return existingRect;

            return HudGraphicFactory.CreateRoot(childName, parent);
        }

        private static T EnsureComponent<T>(Component component) where T : Component
        {
            if (component.TryGetComponent(out T existing))
                return existing;

            return component.gameObject.AddComponent<T>();
        }
    }
}
